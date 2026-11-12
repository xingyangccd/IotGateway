using IotGateway.Common.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IotGateway.WebAPI.Controllers;

[ApiController]
[Route("api/webhooks")]
public sealed class WebhooksController : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "SettingsRead")]
    public IActionResult Get() => Unavailable();

    // Deliberately do not bind, persist or dispatch webhook configuration.
    [HttpPost]
    [Authorize(Policy = "SettingsManage")]
    public IActionResult Create() => Unavailable();

    [HttpPut("{id}")]
    [Authorize(Policy = "SettingsManage")]
    public IActionResult Update() => Unavailable();

    [HttpDelete("{id}")]
    [Authorize(Policy = "SettingsManage")]
    public IActionResult Delete() => Unavailable();

    [HttpPost("{id}/test")]
    [Authorize(Policy = "SettingsManage")]
    public IActionResult Test() => Unavailable();

    private ObjectResult Unavailable() =>
        StatusCode(StatusCodes.Status503ServiceUnavailable,
            ApiResponse.Fail("WebHook 功能尚未启用", StatusCodes.Status503ServiceUnavailable));
}
