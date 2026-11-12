using System.Data;
using System.Net.Mail;
using IotGateway.Application.DTOs;
using IotGateway.Common.Exceptions;
using IotGateway.Domain.Entities;
using IotGateway.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IotGateway.Application.Services;

public sealed class CoreAdministrationService(AppDbContext db) : ICoreAdministrationService
{
    private static readonly string[] BuiltinCodes = ["admin", "engineer", "operator"];

    public async Task<AdministrationPageDto<AdministrationUserDto>> GetUsersAsync(
        int page, int pageSize, string? keyword, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            throw Invalid("分页参数无效（pageSize: 1-100）");
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            keyword = Text(keyword, "关键词", 100);
            query = query.Where(x => x.Username.Contains(keyword) || x.RealName.Contains(keyword));
        }
        var total = await query.CountAsync(ct);
        var users = await query.OrderByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new AdministrationUserDto(x.Id, x.Username, x.RealName, x.Phone, x.Email,
                x.Status, Array.Empty<string>(), x.CreatedAt, x.LastLoginAt)).ToListAsync(ct);
        return new(await WithRolesAsync(users, ct), total, page, pageSize);
    }

    public async Task<AdministrationUserDto> GetUserAsync(long id, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new AdministrationUserDto(x.Id, x.Username, x.RealName, x.Phone, x.Email,
                x.Status, Array.Empty<string>(), x.CreatedAt, x.LastLoginAt)).SingleOrDefaultAsync(ct)
            ?? throw Missing("用户");
        return (await WithRolesAsync([user], ct))[0];
    }

    private async Task<IReadOnlyList<AdministrationUserDto>> WithRolesAsync(
        IReadOnlyList<AdministrationUserDto> users, CancellationToken ct)
    {
        var ids = users.Select(x => x.Id).ToArray();
        var bindings = await (from binding in db.UserRoles.AsNoTracking()
                              join role in db.Roles.AsNoTracking() on binding.RoleId equals role.Id
                              where ids.Contains(binding.UserId)
                              select new { binding.UserId, role.Code }).ToListAsync(ct);
        return users.Select(x => x with
        {
            Roles = bindings.Where(r => r.UserId == x.Id).Select(r => r.Code).Distinct().OrderBy(r => r).ToArray()
        }).ToArray();
    }

    public async Task<AdministrationUserDto> CreateUserAsync(CreateAdministrationUserDto request, CancellationToken ct)
    {
        var username = Username(request.Username);
        var name = Text(request.DisplayName, "显示名称", 50);
        var email = Email(request.Email);
        var phone = Text(request.Phone, "电话", 20);
        var status = Status(request.Status);
        var hash = AdministrationPasswordPolicy.Hash(request.Password);
        await MutateAsync(async () =>
        {
            if (await db.Users.AnyAsync(x => x.Username == username, ct))
                throw Conflict("用户名已存在");
            var roles = await ResolveRolesAsync(request.Roles, ct);
            var user = new User
            {
                Username = username, RealName = name, Email = email, Phone = phone,
                PasswordHash = hash, Status = status, Role = LegacyRole(roles)
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
            db.UserRoles.AddRange(roles.Select(r => new UserRole { UserId = user.Id, RoleId = r.Id }));
            await db.SaveChangesAsync(ct);
        }, ct);
        var id = await db.Users.Where(x => x.Username == username).Select(x => x.Id).SingleAsync(ct);
        return await GetUserAsync(id, ct);
    }

    public async Task UpdateUserAsync(long id, UpdateAdministrationUserDto request, long actorId, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(request.Password))
            throw Invalid("请通过重置密码接口修改密码");
        await MutateAsync(async () =>
        {
            var user = await FindUserAsync(id, ct);
            var roles = request.Roles is null ? null : await ResolveRolesAsync(request.Roles, ct);
            var newStatus = request.Status.HasValue ? Status(request.Status.Value) : user.Status;
            var losesAdmin = roles is not null && roles.All(x => x.Code != "admin");
            if (newStatus == 0 || losesAdmin)
                await ProtectAdminAsync(user, actorId, newStatus == 0, losesAdmin, ct);
            if (request.Username is not null)
            {
                var username = Username(request.Username);
                if (await db.Users.AnyAsync(x => x.Id != id && x.Username == username, ct))
                    throw Conflict("用户名已存在");
                user.Username = username;
            }
            if (request.DisplayName is not null) user.RealName = Text(request.DisplayName, "显示名称", 50);
            if (request.Phone is not null) user.Phone = Text(request.Phone, "电话", 20);
            if (request.Email is not null) user.Email = Email(request.Email);
            user.Status = newStatus;
            user.UpdatedAt = DateTime.UtcNow;
            if (roles is not null)
            {
                var oldBindings = await db.UserRoles.Where(x => x.UserId == id).ToListAsync(ct);
                var wantedIds = roles.Select(x => x.Id).ToHashSet();
                db.UserRoles.RemoveRange(oldBindings.Where(x => !wantedIds.Contains(x.RoleId)));
                db.UserRoles.AddRange(roles.Where(r => oldBindings.All(x => x.RoleId != r.Id))
                    .Select(r => new UserRole { UserId = id, RoleId = r.Id }));
                user.Role = LegacyRole(roles);
            }
            await db.SaveChangesAsync(ct);
        }, ct);
    }

    public Task DeleteUserAsync(long id, long actorId, CancellationToken ct) => MutateAsync(async () =>
    {
        var user = await FindUserAsync(id, ct);
        await ProtectAdminAsync(user, actorId, true, true, ct);
        db.UserRoles.RemoveRange(await db.UserRoles.Where(x => x.UserId == id).ToListAsync(ct));
        db.Users.Remove(user);
        await db.SaveChangesAsync(ct);
    }, ct);

    public async Task ResetPasswordAsync(long id, string password, CancellationToken ct)
    {
        var hash = AdministrationPasswordPolicy.Hash(password);
        await MutateAsync(async () =>
        {
            var user = await FindUserAsync(id, ct);
            if (user.Status != 1)
                throw Conflict("禁用用户不能重置密码，请先启用用户");
            user.PasswordHash = hash;
            user.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }, ct);
    }

    private async Task ProtectAdminAsync(User user, long actorId, bool disabling, bool removingAdmin, CancellationToken ct)
    {
        if (user.Id == actorId && disabling)
            throw Conflict("不能禁用或删除当前登录用户");
        var admins = from binding in db.UserRoles
                     join role in db.Roles on binding.RoleId equals role.Id
                     join member in db.Users on binding.UserId equals member.Id
                     where role.Code == "admin" && role.Status == 1 && member.Status == 1
                     select member.Id;
        if ((disabling || removingAdmin) && await admins.AnyAsync(x => x == user.Id, ct))
        {
            if (user.Id == actorId)
                throw Conflict("不能移除当前登录用户的管理员角色");
            if (!await admins.AnyAsync(x => x != user.Id, ct))
                throw Conflict("必须保留至少一个启用的管理员");
        }
    }

    public async Task<IReadOnlyList<AdministrationRoleDto>> GetRolesAsync(CancellationToken ct)
    {
        var roles = await db.Roles.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(ct);
        var permissions = await (from binding in db.RolePermissions.AsNoTracking()
                                 join permission in db.Permissions.AsNoTracking() on binding.PermissionId equals permission.Id
                                 select new { binding.RoleId, permission.Id, permission.Code }).ToListAsync(ct);
        return roles.Select(r => new AdministrationRoleDto(r.Id, r.Name, r.Code, r.Description, r.Status, r.SortOrder,
            permissions.Where(p => p.RoleId == r.Id).Select(p => p.Id).Distinct().OrderBy(x => x).ToArray(),
            permissions.Where(p => p.RoleId == r.Id).Select(p => p.Code).Distinct().OrderBy(x => x).ToArray())).ToArray();
    }

    public async Task<AdministrationRoleDto> GetRoleAsync(long id, CancellationToken ct) =>
        (await GetRolesAsync(ct)).SingleOrDefault(x => x.Id == id) ?? throw Missing("角色");

    public Task UpdateRoleAsync(long id, UpdateAdministrationRoleDto request, CancellationToken ct) => MutateAsync(async () =>
    {
        var role = await db.Roles.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Missing("角色");
        if (!BuiltinCodes.Contains(role.Code))
            throw Conflict("不支持自定义角色，请迁移到内置角色");
        if (request.Code is not null && request.Code != role.Code)
            throw Conflict("不能修改内置角色编码");
        if (request.Status.HasValue && request.Status != 1)
            throw Conflict("不能禁用内置角色");
        if (request.Name is not null) role.Name = Text(request.Name, "角色名称", 50, required: true);
        if (request.Description is not null) role.Description = Text(request.Description, "描述", 200);
        if (request.SortOrder.HasValue)
        {
            if (request.SortOrder is < 0 or > 10000) throw Invalid("排序值必须为0-10000");
            role.SortOrder = request.SortOrder.Value;
        }
        if (request.Status.HasValue) role.Status = 1;
        if (request.PermissionIds is not null || request.Permissions is not null)
        {
            if (request.PermissionIds is not null && request.Permissions is not null)
                throw Invalid("permissionIds与permissions不能同时提交");
            var permissions = await ResolvePermissionsAsync(request, ct);
            var oldBindings = await db.RolePermissions.Where(x => x.RoleId == id).ToListAsync(ct);
            var wanted = permissions.Select(x => x.Id).ToHashSet();
            db.RolePermissions.RemoveRange(oldBindings.Where(x => !wanted.Contains(x.PermissionId)));
            db.RolePermissions.AddRange(permissions.Where(p => oldBindings.All(x => x.PermissionId != p.Id))
                .Select(p => new RolePermission { RoleId = id, PermissionId = p.Id }));
        }
        await db.SaveChangesAsync(ct);
    }, ct);

    public Task DeleteRoleAsync(long id, CancellationToken ct) => MutateAsync(async () =>
    {
        var role = await db.Roles.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Missing("角色");
        if (BuiltinCodes.Contains(role.Code, StringComparer.OrdinalIgnoreCase))
            throw Conflict("不能删除内置角色");
        if (await db.UserRoles.AnyAsync(x => x.RoleId == id, ct))
            throw Conflict("角色仍有用户绑定，不能删除");
        db.RolePermissions.RemoveRange(await db.RolePermissions.Where(x => x.RoleId == id).ToListAsync(ct));
        db.Roles.Remove(role);
        await db.SaveChangesAsync(ct);
    }, ct);

    public async Task<IReadOnlyList<AdministrationPermissionDto>> GetPermissionsAsync(CancellationToken ct) =>
        await db.Permissions.AsNoTracking().OrderBy(x => x.Resource).ThenBy(x => x.Code)
            .Select(x => new AdministrationPermissionDto(x.Id, x.Name, x.Code, x.Resource, x.Action, x.Description))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AdministrationPermissionGroupDto>> GetPermissionTreeAsync(CancellationToken ct) =>
        (await GetPermissionsAsync(ct)).GroupBy(x => x.Resource)
            .Select(g => new AdministrationPermissionGroupDto(
                string.IsNullOrEmpty(g.Key) ? "其他" : g.Key, g.Key, g.ToArray())).ToArray();

    private async Task<List<Role>> ResolveRolesAsync(string[]? codes, CancellationToken ct)
    {
        if (codes is null || codes.Length is < 1 or > 3 || codes.Any(x => !BuiltinCodes.Contains(x))
            || codes.Distinct().Count() != codes.Length)
            throw Invalid("角色只能是admin、engineer、operator，至少选择一个且不能重复");
        var roles = await db.Roles.Where(x => codes.Contains(x.Code) && x.Status == 1).ToListAsync(ct);
        if (roles.Count != codes.Length) throw Invalid("角色不存在或已禁用");
        return roles;
    }

    private async Task<List<Permission>> ResolvePermissionsAsync(UpdateAdministrationRoleDto request, CancellationToken ct)
    {
        if (request.PermissionIds is { } ids)
        {
            if (ids.Length > 200 || ids.Any(x => x <= 0) || ids.Distinct().Count() != ids.Length)
                throw Invalid("权限ID无效或重复");
            var permissions = await db.Permissions.Where(x => ids.Contains(x.Id)).ToListAsync(ct);
            if (permissions.Count != ids.Length) throw Invalid("权限不存在");
            return permissions;
        }
        var codes = request.Permissions!;
        if (codes.Length > 200 || codes.Any(string.IsNullOrWhiteSpace) || codes.Any(x => x.Length > 100)
            || codes.Distinct(StringComparer.Ordinal).Count() != codes.Length)
            throw Invalid("权限编码无效或重复");
        var matches = await db.Permissions.Where(x => codes.Contains(x.Code)).ToListAsync(ct);
        if (matches.Count != codes.Length || matches.Any(x => !codes.Contains(x.Code, StringComparer.Ordinal)))
            throw Invalid("权限不存在");
        return matches;
    }

    private async Task<User> FindUserAsync(long id, CancellationToken ct) =>
        await db.Users.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Missing("用户");

    private async Task MutateAsync(Func<Task> action, CancellationToken ct)
    {
        // Serializable reads protect administrator invariants against concurrent demotions/deletions.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            await action();
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw Conflict("数据冲突或存在关联记录，请刷新后重试");
        }
        catch (System.Data.Common.DbException)
        {
            throw Conflict("数据库操作冲突，请刷新后重试");
        }
    }

    private static string LegacyRole(IReadOnlyCollection<Role> roles) =>
        roles.Any(x => x.Code == "admin") ? "Admin" : roles.Any(x => x.Code == "engineer") ? "Engineer" : "Operator";

    private static string Username(string? value)
    {
        var name = Text(value, "用户名", 50, required: true);
        if (name.Any(x => !(char.IsLetterOrDigit(x) || x is '_' or '-' or '.' or '@')))
            throw Invalid("用户名只能包含字母、数字、下划线、连字符、点或@");
        return name;
    }

    private static string Text(string? value, string field, int maxLength, bool required = false)
    {
        value = value?.Trim() ?? "";
        if (value.Length > maxLength || value.Any(char.IsControl) || (required && value.Length == 0))
            throw Invalid($"{field}无效或超过{maxLength}个字符");
        return value;
    }

    private static string Email(string? value)
    {
        var email = Text(value, "邮箱", 100);
        if (email.Length != 0 && (!MailAddress.TryCreate(email, out var address) || address.Address != email))
            throw Invalid("邮箱格式无效");
        return email;
    }

    private static sbyte Status(int value) => value is 0 or 1 ? (sbyte)value : throw Invalid("状态只能为0或1");
    private static BusinessException Invalid(string message) => new(message, 422);
    private static BusinessException Conflict(string message) => new(message, 409);
    private static NotFoundException Missing(string resource) => new($"{resource}不存在");
}
