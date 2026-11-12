using IotGateway.Application.Services;
using IotGateway.Common.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IotGateway.WebAPI.Controllers;

[ApiController]
[Route("api/permissions")]
[Authorize(Policy = "UserManage")]
public sealed class PermissionsController(ICoreAdministrationService administration) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(ApiResponse.Ok(await administration.GetPermissionsAsync(ct)));

    [HttpGet("tree")]
    public async Task<IActionResult> Tree(CancellationToken ct) =>
        Ok(ApiResponse.Ok(await administration.GetPermissionTreeAsync(ct)));
}
