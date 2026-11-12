using IotGateway.Common.Helpers;
using IotGateway.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IotGateway.WebAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/sys-dict/device_type")]
public sealed class DeviceTypesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var types = await db.Devices.Select(d => d.Category).Union(db.AlarmRules.Select(r => r.DeviceType))
            .Where(type => type != "").Distinct().OrderBy(type => type).ToArrayAsync(cancellationToken);
        return Ok(ApiResponse.Ok(types.Select(type => new { label = type, value = type }).ToArray()));
    }
}
