using IotGateway.Application.DTOs;
using IotGateway.Application.Services;
using IotGateway.Common.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IotGateway.WebAPI.Controllers;

[ApiController]
[Authorize(Policy = "AuditRead")]
[Route("api/audit-logs")]
[Route("api/audit")]
public sealed class AuditController : ControllerBase
{
    private readonly IAuditService _audit;

    public AuditController(IAuditService audit) => _audit = audit;

    [HttpGet]
    public async Task<IActionResult> GetLogs(
        [FromQuery] AuditLogQueryDto query, CancellationToken cancellationToken) =>
        Ok(ApiResponse<AuditPageDto>.Ok(await _audit.GetLogsAsync(query, cancellationToken)));
}
