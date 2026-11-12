using IotGateway.Application.DTOs;

namespace IotGateway.Application.Services;

public interface ICoreAdministrationService
{
    Task<AdministrationPageDto<AdministrationUserDto>> GetUsersAsync(int page, int pageSize, string? keyword, CancellationToken ct);
    Task<AdministrationUserDto> GetUserAsync(long id, CancellationToken ct);
    Task<AdministrationUserDto> CreateUserAsync(CreateAdministrationUserDto request, CancellationToken ct);
    Task UpdateUserAsync(long id, UpdateAdministrationUserDto request, long actorId, CancellationToken ct);
    Task DeleteUserAsync(long id, long actorId, CancellationToken ct);
    Task ResetPasswordAsync(long id, string password, CancellationToken ct);
    Task<IReadOnlyList<AdministrationRoleDto>> GetRolesAsync(CancellationToken ct);
    Task<AdministrationRoleDto> GetRoleAsync(long id, CancellationToken ct);
    Task UpdateRoleAsync(long id, UpdateAdministrationRoleDto request, CancellationToken ct);
    Task DeleteRoleAsync(long id, CancellationToken ct);
    Task<IReadOnlyList<AdministrationPermissionDto>> GetPermissionsAsync(CancellationToken ct);
    Task<IReadOnlyList<AdministrationPermissionGroupDto>> GetPermissionTreeAsync(CancellationToken ct);
}
