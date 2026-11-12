using System.ComponentModel.DataAnnotations;
using IotGateway.Application.DTOs;
using IotGateway.Common.Exceptions;
using IotGateway.Common.Helpers;
using IotGateway.Domain.Entities;
using IotGateway.Infrastructure.Data;
using IotGateway.WebAPI.Filters;
using IotGateway.WebAPI.Hubs;
using IotGateway.WebAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace IotGateway.WebAPI.Controllers;

[ApiController]
[Authorize]
[AlarmApiValidation]
[Route("api/devices")]
public sealed class DevicesController(AppDbContext db, IHubContext<DeviceHub> hub,
    ILogger<DevicesController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] DeviceQueryRequest request, CancellationToken cancellationToken)
    {
        var query = db.Devices.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(d => d.DeviceCode.Contains(keyword) || d.Name.Contains(keyword));
        }
        if (!string.IsNullOrWhiteSpace(request.DeviceType))
            query = query.Where(d => d.Category == request.DeviceType.Trim());
        if (request.Status.HasValue)
            query = query.Where(d => d.ConnectionStatus == request.Status.Value);
        var total = await query.CountAsync(cancellationToken);
        var devices = await query.OrderByDescending(d => d.CreatedAt).ThenByDescending(d => d.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(cancellationToken);
        return Ok(ApiResponse<DevicePageDto>.Ok(new(devices.Select(DeviceApiQueries.ToDto).ToArray(),
            total, request.Page, request.PageSize)));
    }

    [HttpGet("realtime")]
    public async Task<IActionResult> Realtime(CancellationToken cancellationToken) =>
        Ok(ApiResponse<DeviceRealtimeDto[]>.Ok(await DeviceApiQueries.RealtimeAsync(db, null, cancellationToken)));

    [HttpGet("{id:long}")]
    [HttpGet("{id:long}/detail")]
    public async Task<IActionResult> Detail(long id, CancellationToken cancellationToken)
    {
        var device = await db.Devices.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new NotFoundException("设备", id);
        return Ok(ApiResponse<DeviceDto>.Ok(DeviceApiQueries.ToDto(device)));
    }

    [HttpGet("{id:long}/history")]
    public async Task<IActionResult> History(long id, [FromQuery] DeviceHistoryRequest request,
        CancellationToken cancellationToken)
    {
        if (!await db.Devices.AnyAsync(d => d.Id == id, cancellationToken))
            throw new NotFoundException("设备", id);
        var key = request.DataKey.Trim();
        var start = request.StartTime!.Value.UtcDateTime;
        var end = request.EndTime!.Value.UtcDateTime;
        var rows = await db.DeviceData.AsNoTracking().Where(d => d.DeviceId == id && d.ParamName == key &&
                d.CollectedAt >= start && d.CollectedAt <= end)
            .OrderBy(d => d.CollectedAt).ThenBy(d => d.Id).Take(10001).ToListAsync(cancellationToken);
        if (rows.Count > 10000)
            throw new AppException("历史数据超过 10000 条，请缩小查询时间范围。", 422);
        return Ok(ApiResponse<DeviceHistoryDto[]>.Ok(rows.Select(DeviceApiQueries.ToDto).ToArray()));
    }

    [HttpPost]
    [Authorize(Policy = "DeviceWrite")]
    public async Task<IActionResult> Create([FromBody, Required] DeviceCreateRequest request,
        CancellationToken cancellationToken)
    {
        var actor = AlarmApiValidationAttribute.ActorId(User);
        var result = await DeviceTransactions.RunAsync(db, async () =>
        {
            var device = new Device { CreatedBy = actor, CreatedAt = DateTime.UtcNow };
            DeviceApiQueries.Apply(request, device);
            db.Devices.Add(device);
            await db.SaveChangesAsync(cancellationToken);
            return DeviceApiQueries.ToDto(device);
        }, cancellationToken);
        await PublishDeviceAsync(result.Id);
        return Ok(ApiResponse<DeviceDto>.Ok(result));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = "DeviceWrite")]
    public async Task<IActionResult> Update(long id, [FromBody, Required] DeviceWriteRequest request,
        CancellationToken cancellationToken)
    {
        var result = await DeviceTransactions.RunAsync(db, async () =>
        {
            await DeviceTransactions.LockAsync(db, id, cancellationToken);
            var device = await db.Devices.SingleAsync(d => d.Id == id, cancellationToken);
            DeviceApiQueries.Apply(request, device);
            await db.SaveChangesAsync(cancellationToken);
            return DeviceApiQueries.ToDto(device);
        }, cancellationToken);
        await PublishDeviceAsync(id);
        return Ok(ApiResponse<DeviceDto>.Ok(result));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = "DeviceWrite")]
    public Task<IActionResult> Delete(long id, CancellationToken cancellationToken) =>
        DeleteDevicesAsync([id], cancellationToken);

    [HttpPost("batch-delete")]
    [Authorize(Policy = "DeviceWrite")]
    public Task<IActionResult> BatchDelete([FromBody, Required] DeviceBatchDeleteRequest request,
        CancellationToken cancellationToken) => DeleteDevicesAsync(request.Ids, cancellationToken);

    [HttpPost("{id:long}/telemetry")]
    [Authorize(Policy = "DeviceWrite")]
    public async Task<IActionResult> Telemetry(long id, [FromBody, Required] DeviceTelemetryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await DeviceTelemetryProcessor.PersistAsync(db, id, request, cancellationToken);
        await PublishDeviceAsync(id, result.Alarms);
        return Ok(ApiResponse<DeviceTelemetryResult>.Ok(result));
    }

    [HttpPost("/api/telemetry")]
    [Authorize(Policy = "DeviceWrite")]
    public Task<IActionResult> TelemetryById([FromBody, Required] DeviceTelemetryByIdRequest request,
        CancellationToken cancellationToken) => Telemetry(request.DeviceId, request, cancellationToken);

    private async Task<IActionResult> DeleteDevicesAsync(long[] ids, CancellationToken cancellationToken)
    {
        var distinctIds = ids.Distinct().Order().ToArray();
        if (distinctIds.Length is < 1 or > 200 || distinctIds.Any(id => id <= 0))
            throw new AppException("必须提供 1 到 200 个有效设备 ID。", 400);
        var deleted = await DeviceTransactions.RunAsync(db, async () =>
        {
            foreach (var id in distinctIds)
                await DeviceTransactions.LockAsync(db, id, cancellationToken);
            if (await HasDependenciesAsync(distinctIds, cancellationToken))
                throw new AppException("设备有关联的遥测、告警、配置、生产或维护记录，不能删除。", 409);
            var devices = await db.Devices.Where(d => distinctIds.Contains(d.Id)).ToListAsync(cancellationToken);
            db.Devices.RemoveRange(devices);
            await db.SaveChangesAsync(cancellationToken);
            return devices;
        }, cancellationToken);
        try
        {
            foreach (var device in deleted)
                await hub.Clients.All.SendAsync("DeviceDeleted", new { device.Id, device.DeviceCode });
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Device deletion committed but broadcast failed");
        }
        return Ok(ApiResponse.Ok(new { deleted = deleted.Count }));
    }

    private async Task<bool> HasDependenciesAsync(long[] ids, CancellationToken token) =>
        await db.DeviceData.AnyAsync(x => ids.Contains(x.DeviceId), token) ||
        await db.Alarms.AnyAsync(x => ids.Contains(x.DeviceId), token) ||
        await db.AlarmRules.AnyAsync(x => ids.Contains(x.DeviceId), token) ||
        await db.DeviceConfigs.AnyAsync(x => ids.Contains(x.DeviceId), token) ||
        await db.ProductionRecords.AnyAsync(x => ids.Contains(x.DeviceId), token) ||
        await db.EnergyRecords.AnyAsync(x => ids.Contains(x.DeviceId), token) ||
        await db.MaintenancePlans.AnyAsync(x => ids.Contains(x.DeviceId), token) ||
        await db.MaintenanceRecords.AnyAsync(x => ids.Contains(x.DeviceId), token) ||
        await db.RepairOrders.AnyAsync(x => ids.Contains(x.DeviceId), token) ||
        await db.DeviceHealthScores.AnyAsync(x => ids.Contains(x.DeviceId), token) ||
        (db.Model.FindEntityType(typeof(MesLine)) is not null &&
         await db.Set<MesLine>().AnyAsync(x => x.DeviceId.HasValue && ids.Contains(x.DeviceId.Value), token));

    private async Task PublishDeviceAsync(long id, IReadOnlyList<AlarmDto>? alarms = null)
    {
        // SignalR is a notification channel; a delivery failure must never roll back an accepted reading.
        try
        {
            var data = await DeviceApiQueries.RealtimeAsync(db, id, CancellationToken.None);
            await hub.Clients.All.SendAsync("DeviceDataUpdated", data);
            await hub.Clients.All.SendAsync("ReceiveDeviceData", data);
            foreach (var device in data)
                await hub.Clients.All.SendAsync("DeviceStatusChanged",
                    new { device.DeviceCode, newStatus = device.Status });
            if (alarms is not { Count: > 0 }) return;
            foreach (var alarm in alarms)
            {
                await hub.Clients.All.SendAsync("NewAlarm", alarm);
                await hub.Clients.All.SendAsync("ReceiveAlarm", alarm);
            }
            var count = await db.Alarms.CountAsync(a => a.Status.ToLower() == "active" ||
                                                       a.Status.ToLower() == "unconfirmed");
            await hub.Clients.All.SendAsync("AlarmCountUpdated", count);
            await hub.Clients.All.SendAsync("ReceiveAlarmCount", count);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Device update committed but broadcast failed");
        }
    }
}
