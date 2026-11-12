using IotGateway.Application.DTOs;
using IotGateway.Application.Services;
using IotGateway.Common.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IotGateway.WebAPI.Controllers;

[ApiController]
[Route("api/settings")]
public sealed class SettingsController : ControllerBase
{
    private readonly ISettingsService _settings;

    public SettingsController(ISettingsService settings) => _settings = settings;

    [HttpGet("basic")]
    [Authorize(Policy = "SettingsRead")]
    public async Task<IActionResult> GetBasic(CancellationToken cancellationToken) =>
        Ok(ApiResponse<BasicSettingsDto>.Ok(await _settings.GetBasicAsync(cancellationToken)));

    [HttpPut("basic")]
    [Authorize(Policy = "SettingsManage")]
    public async Task<IActionResult> UpdateBasic(
        [FromBody] UpdateBasicSettingsDto settings, CancellationToken cancellationToken) =>
        Ok(ApiResponse<BasicSettingsDto>.Ok(
            await _settings.UpdateBasicAsync(settings, cancellationToken)));
}
