using IotGateway.Application.DTOs;
using IotGateway.Application.Services;
using IotGateway.Common.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IotGateway.WebAPI.Controllers;

[ApiController]
[Route("api/roles")]
[Authorize(Policy = "UserManage")]
public sealed class RolesController(ICoreAdministrationService administration) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(ApiResponse.Ok(await administration.GetRolesAsync(ct)));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await administration.GetRoleAsync(id, ct)));

    [HttpPost]
    public IActionResult Create() =>
        Conflict(ApiResponse.Fail("仅支持由系统初始化的admin、engineer、operator角色；MES不支持自定义角色编码", 409));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, UpdateAdministrationRoleDto request, CancellationToken ct)
    {
        await administration.UpdateRoleAsync(id, request, ct);
        return Ok(ApiResponse.Ok());
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await administration.DeleteRoleAsync(id, ct);
        return Ok(ApiResponse.Ok());
    }
}
