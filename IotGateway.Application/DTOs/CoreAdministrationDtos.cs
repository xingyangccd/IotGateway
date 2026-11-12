namespace IotGateway.Application.DTOs;

public sealed record AdministrationPageDto<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public sealed record AdministrationUserDto(
    long Id, string Username, string DisplayName, string Phone, string Email,
    int Status, IReadOnlyList<string> Roles, DateTime CreatedAt, DateTime? LastLoginAt);

public sealed class CreateAdministrationUserDto
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public int Status { get; set; } = 1;
    public string[] Roles { get; set; } = ["operator"];
}

public sealed class UpdateAdministrationUserDto
{
    public string? Username { get; set; }
    public string? DisplayName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public int? Status { get; set; }
    public string[]? Roles { get; set; }
    // The existing edit form sends an empty password; nonempty changes use reset-password.
    public string? Password { get; set; }
}

public sealed class ResetAdministrationPasswordDto
{
    public string Password { get; set; } = "";
}

public sealed class AdministrationUserStatusDto
{
    public int? Status { get; set; }
}

public sealed record AdministrationRoleDto(
    long Id, string Name, string Code, string Description, int Status, int SortOrder,
    IReadOnlyList<long> PermissionIds, IReadOnlyList<string> Permissions);

public sealed class UpdateAdministrationRoleDto
{
    public string? Name { get; set; }
    public string? Code { get; set; }
    public string? Description { get; set; }
    public int? Status { get; set; }
    public int? SortOrder { get; set; }
    public long[]? PermissionIds { get; set; }
    public string[]? Permissions { get; set; }
}

public sealed record AdministrationPermissionDto(
    long Id, string Name, string Code, string Resource, string Action, string Description);

public sealed record AdministrationPermissionGroupDto(
    string Name, string Resource, IReadOnlyList<AdministrationPermissionDto> Children);
