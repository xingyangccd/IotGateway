using System.Security.Claims;
using IotGateway.Application.DTOs;
using IotGateway.Application.Services;
using IotGateway.Common.Exceptions;
using IotGateway.Common.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IotGateway.WebAPI.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = "UserManage")]
public sealed class UsersController(ICoreAdministrationService administration) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, [FromQuery] string? keyword = null) =>
        Ok(ApiResponse.Ok(await administration.GetUsersAsync(page, pageSize, keyword, ct)));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await administration.GetUserAsync(id, ct)));

    [HttpPost]
    public async Task<IActionResult> Create(CreateAdministrationUserDto request, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await administration.CreateUserAsync(request, ct)));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, UpdateAdministrationUserDto request, CancellationToken ct)
    {
        await administration.UpdateUserAsync(id, request, ActorId(), ct);
        return Ok(ApiResponse.Ok());
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await administration.DeleteUserAsync(id, ActorId(), ct);
        return Ok(ApiResponse.Ok());
    }

    [HttpPut("{id:long}/reset-password")]
    public async Task<IActionResult> ResetPassword(long id, ResetAdministrationPasswordDto request, CancellationToken ct)
    {
        await administration.ResetPasswordAsync(id, request.Password, ct);
        return Ok(ApiResponse.Ok());
    }

    [HttpPut("{id:long}/toggle-status")]
    public async Task<IActionResult> ToggleStatus(long id, AdministrationUserStatusDto request, CancellationToken ct)
    {
        if (!request.Status.HasValue) throw new BusinessException("必须提供status", 422);
        await administration.UpdateUserAsync(id, new() { Status = request.Status }, ActorId(), ct);
        return Ok(ApiResponse.Ok());
    }

    private long ActorId() =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) && id > 0
            ? id : throw new UnauthorizedException("登录身份无效");
}
