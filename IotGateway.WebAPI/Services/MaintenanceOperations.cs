using System.Data;
using System.Security.Claims;
using System.Text.Json;
using IotGateway.Common.Exceptions;
using IotGateway.Common.Helpers;
using IotGateway.Domain.Entities;
using IotGateway.Infrastructure.Data;
using IotGateway.WebAPI.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace IotGateway.WebAPI.Services;

[AttributeUsage(AttributeTargets.Class)]
public sealed class MaintenanceValidationAttribute : ActionFilterAttribute
{
    public MaintenanceValidationAttribute() => Order = -3000;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        // Run before ApiController's automatic 400 response.
        if (!context.ModelState.IsValid)
            context.Result = new ObjectResult(ApiResponse.Fail("请求参数格式无效", 422)) { StatusCode = 422 };
    }
}

internal static class MaintenanceOperations
{
    internal static readonly string[] RepairStates = ["Pending", "Assigned", "Repairing", "Completed", "Verified", "Closed"];

    internal static void Require(bool condition, string message, int status = 422)
    {
        if (!condition) throw new BusinessException(message, status);
    }

    internal static string Text(string? value, string field, int maxLength = 10000, bool required = false)
    {
        var text = value?.Trim() ?? "";
        Require(!required || text.Length > 0, $"{field}不能为空");
        Require(text.Length <= maxLength, $"{field}长度不能超过{maxLength}");
        return text;
    }

    internal static string Choice(string? value, string field, params string[] choices)
    {
        var result = choices.FirstOrDefault(x => string.Equals(x, value?.Trim(), StringComparison.OrdinalIgnoreCase));
        Require(result != null, $"{field}必须为: {string.Join(", ", choices)}");
        return result!;
    }

    internal static string? Json(string? value, string field, bool arrayOnly = false, bool required = false)
    {
        var text = Text(value, field, 16000, required);
        if (text.Length == 0) return null;
        try
        {
            using var json = JsonDocument.Parse(text);
            Require(arrayOnly
                ? json.RootElement.ValueKind == JsonValueKind.Array && (!required || json.RootElement.GetArrayLength() > 0)
                : json.RootElement.ValueKind is JsonValueKind.Array or JsonValueKind.Object,
                $"{field}必须为{(arrayOnly ? "非空JSON数组" : "JSON对象或数组")}");
        }
        catch (JsonException)
        {
            throw new BusinessException($"{field}不是有效JSON", 422);
        }
        return text;
    }

    internal static void Cost(decimal value)
    {
        Require(value >= 0 && value <= 99999999.99m && decimal.Round(value, 2) == value,
            "费用必须为0至99999999.99，最多两位小数");
    }

    internal static async Task DeviceAsync(AppDbContext db, long id, CancellationToken ct)
    {
        Require(id > 0, "设备ID必须大于0");
        Require(await db.Set<Device>().AnyAsync(x => x.Id == id, ct), "设备不存在", 404);
    }

    internal sealed record Actor(long Id, string Name);

    internal static async Task<Actor> ActorAsync(AppDbContext db, ClaimsPrincipal principal, CancellationToken ct)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
        Require(long.TryParse(value, out var id) && id > 0, "身份声明无效", 401);
        return await EligibleUserAsync(db, id, true, ct);
    }

    internal static async Task<Actor> EligibleUserAsync(AppDbContext db, long id, bool actor, CancellationToken ct)
    {
        Require(id > 0, "用户ID必须大于0");
        var user = await db.Set<User>().AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.Id, x.Username, x.RealName, x.Role, x.Status }).SingleOrDefaultAsync(ct);
        Require(user != null, actor ? "当前用户不存在" : "指派用户不存在", actor ? 401 : 404);
        Require(user!.Status == 1 && new[] { "Admin", "Engineer", "Operator" }
            .Contains(user.Role, StringComparer.OrdinalIgnoreCase),
            "用户必须启用且具有Admin、Engineer或Operator角色", actor ? 403 : 422);
        return new Actor(user.Id, string.IsNullOrWhiteSpace(user.RealName) ? user.Username : user.RealName);
    }

    internal static async Task<T> WriteAsync<T>(AppDbContext db, Func<Task<T>> action, CancellationToken ct)
    {
        try
        {
            // Validation reads and writes share a transaction, including plan history and assignee checks.
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var result = await action();
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessException("数据已被其他请求修改，请刷新后重试", 409);
        }
        catch (Exception ex) when (IsConflict(ex))
        {
            throw new BusinessException("数据冲突，请刷新后重试", 409);
        }
    }

    private static bool IsConflict(Exception exception)
    {
        // Pomelo may wrap transient MySQL errors in its execution strategy.
        for (Exception? current = exception; current != null; current = current.InnerException)
            if (current is MySqlException mysql && mysql.Number is 1062 or 1205 or 1213 or 1451 or 1452 or 3572)
                return true;
        return false;
    }

    internal static async Task<MaintenancePage<T>> PageAsync<T>(IQueryable<T> query, int page, int pageSize, CancellationToken ct)
    {
        Require(page >= 1 && pageSize is >= 1 and <= 100 && (long)(page - 1) * pageSize <= int.MaxValue,
            "page必须大于0，pageSize必须为1至100，分页偏移不能超出范围");
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new MaintenancePage<T>(items, total, page, pageSize);
    }

    internal static (DateTime? From, DateTime? To) Dates(DateTimeOffset? from, DateTimeOffset? to)
    {
        Require(!from.HasValue || from.Value.UtcDateTime.Year >= 1000, "开始时间无效");
        Require(!to.HasValue || to.Value.UtcDateTime.Year >= 1000, "结束时间无效");
        Require(!from.HasValue || !to.HasValue || from <= to, "开始时间不能晚于结束时间");
        return (from?.UtcDateTime, to?.UtcDateTime);
    }

    internal static async Task<MaintenancePlan> PlanAsync(AppDbContext db, long id, CancellationToken ct)
    {
        Require(id > 0, "计划ID必须大于0");
        return await db.Set<MaintenancePlan>().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new BusinessException("维保计划不存在", 404);
    }

    internal static async Task<RepairOrder> RepairAsync(AppDbContext db, long id, CancellationToken ct)
    {
        Require(id > 0, "工单ID必须大于0");
        return await db.Set<RepairOrder>().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new BusinessException("维修工单不存在", 404);
    }

    internal static void State(RepairOrder order, params string[] states) =>
        Require(states.Contains(order.Status), $"当前状态{order.Status}不允许此操作", 409);
}
