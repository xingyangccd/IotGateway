using IotGateway.Application.DTOs;
using IotGateway.Application.Services;
using IotGateway.Common.Exceptions;
using IotGateway.Common.Helpers;
using IotGateway.Domain.Entities;
using IotGateway.Infrastructure.Data;
using IotGateway.WebAPI.Filters;
using IotGateway.WebAPI.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace IotGateway.WebAPI.Controllers;

[ApiController]
[Authorize]
[AlarmApiValidation]
[Route("api/alarms")]
public sealed class AlarmsController(AppDbContext db, IHubContext<DeviceHub> hub,
    ILogger<AlarmsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] AlarmQueryDto request, CancellationToken cancellationToken)
    {
        var query = db.Alarms.AsNoTracking();
        if (request.Level.HasValue)
            query = WithLevel(query, request.Level.Value);
        if (!string.IsNullOrEmpty(request.Status))
            query = request.Status switch
            {
                "unconfirmed" => Unconfirmed(query),
                "confirmed" => query.Where(a => a.Status.ToLower() == "confirmed"),
                "resolved" => query.Where(a => a.Status.ToLower() == "resolved" || a.Status.ToLower() == "closed"),
                _ => query
            };
        if (!string.IsNullOrWhiteSpace(request.DeviceCode))
        {
            var code = request.DeviceCode.Trim();
            query = query.Where(a => db.Devices.Any(d => d.Id == a.DeviceId && d.DeviceCode == code));
        }
        if (request.StartDate.HasValue)
        {
            var start = DateOnly.TryParseExact(Request.Query["startDate"], "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var startDay)
                ? startDay.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
                : request.StartDate.Value.UtcDateTime;
            query = query.Where(a => a.CreatedAt >= start);
        }
        if (request.EndDate.HasValue)
        {
            if (DateOnly.TryParseExact(Request.Query["endDate"], "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var endDay))
            {
                // The Vue date picker sends calendar days, not instants.
                if (endDay != DateOnly.MaxValue)
                {
                    var endExclusive = endDay.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                    query = query.Where(a => a.CreatedAt < endExclusive);
                }
            }
            else
            {
                var end = request.EndDate.Value.UtcDateTime;
                query = query.Where(a => a.CreatedAt <= end);
            }
        }

        var total = await query.CountAsync(cancellationToken);
        var alarms = await query.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(cancellationToken);
        var deviceIds = alarms.Select(a => a.DeviceId).Distinct().ToArray();
        var devices = await db.Devices.AsNoTracking().Where(d => deviceIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, cancellationToken);
        var userIds = alarms.SelectMany(a => new[] { a.ConfirmedBy, a.ResolvedBy })
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Username, cancellationToken);
        var items = alarms.Select(a => AlarmRuleEvaluator.ToDto(a, devices.GetValueOrDefault(a.DeviceId),
            a.ConfirmedBy.HasValue ? users.GetValueOrDefault(a.ConfirmedBy.Value) : null,
            a.ResolvedBy.HasValue ? users.GetValueOrDefault(a.ResolvedBy.Value) : null)).ToArray();
        return Ok(ApiResponse<AlarmPageDto>.Ok(new AlarmPageDto(items, total, request.Page, request.PageSize)));
    }

    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        var start = today.AddDays(-6);
        var query = db.Alarms.AsNoTracking();
        var counts = await query.Where(a => a.CreatedAt >= start && a.CreatedAt < tomorrow)
            .GroupBy(a => a.CreatedAt.Date)
            .Select(group => new { Day = group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.Day, x => x.Count, cancellationToken);
        var trend = Enumerable.Range(0, 7).Select(i => counts.GetValueOrDefault(start.AddDays(i))).ToArray();
        var unconfirmed = await Unconfirmed(query).CountAsync(cancellationToken);
        var serious = await WithLevel(query, 2).CountAsync(cancellationToken);
        return Ok(ApiResponse<AlarmStatsDto>.Ok(new AlarmStatsDto(trend[6], unconfirmed, serious, trend)));
    }

    [HttpPut("{id:long}/confirm")]
    [Authorize(Policy = "AlarmWrite")]
    public async Task<IActionResult> Confirm(long id, CancellationToken cancellationToken)
    {
        var actor = AlarmApiValidationAttribute.ActorId(User);
        var now = DateTime.UtcNow;
        var affected = await Unconfirmed(db.Alarms.Where(a => a.Id == id))
            .ExecuteUpdateAsync(update => update
                .SetProperty(a => a.Status, "Confirmed")
                .SetProperty(a => a.ConfirmedBy, (long?)actor)
                .SetProperty(a => a.ConfirmedAt, (DateTime?)now), cancellationToken);
        await EnsureTransitionAsync(id, affected, cancellationToken);
        await PublishCountAsync();
        return Ok(ApiResponse.Ok());
    }

    [HttpPut("{id:long}/resolve")]
    [Authorize(Policy = "AlarmWrite")]
    public async Task<IActionResult> Resolve(long id, [FromBody, Required] ResolveAlarmRequest request,
        CancellationToken cancellationToken)
    {
        var actor = AlarmApiValidationAttribute.ActorId(User);
        var now = DateTime.UtcNow;
        var affected = await db.Alarms.Where(a => a.Id == id &&
                (a.Status.ToLower() == "active" || a.Status.ToLower() == "unconfirmed" ||
                 a.Status.ToLower() == "confirmed"))
            .ExecuteUpdateAsync(update => update
                .SetProperty(a => a.Status, "Resolved")
                .SetProperty(a => a.ResolvedBy, (long?)actor)
                .SetProperty(a => a.ResolvedAt, (DateTime?)now)
                .SetProperty(a => a.ResolveRemark, request.Remark), cancellationToken);
        await EnsureTransitionAsync(id, affected, cancellationToken);
        await PublishCountAsync();
        return Ok(ApiResponse.Ok());
    }

    private async Task EnsureTransitionAsync(long id, int affected, CancellationToken cancellationToken)
    {
        if (affected != 0)
            return;
        if (!await db.Alarms.AnyAsync(a => a.Id == id, cancellationToken))
            throw new NotFoundException("告警", id);
        throw new AppException("告警状态已变化，无法重复确认或解决已处理的告警。", 409);
    }

    private async Task PublishCountAsync()
    {
        // The transition is already committed; a disconnected client must not turn it into an HTTP failure.
        try
        {
            var count = await Unconfirmed(db.Alarms).CountAsync();
            await Task.WhenAll(hub.Clients.All.SendAsync("AlarmCountUpdated", count),
                hub.Clients.All.SendAsync("ReceiveAlarmCount", count));
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to broadcast updated alarm count");
        }
    }

    private static IQueryable<Alarm> Unconfirmed(IQueryable<Alarm> query) =>
        query.Where(a => a.Status.ToLower() == "active" || a.Status.ToLower() == "unconfirmed");

    private static IQueryable<Alarm> WithLevel(IQueryable<Alarm> query, int level) => level switch
    {
        2 => query.Where(a => a.Level != null &&
            (a.Level.ToLower() == "critical" || a.Level.ToLower() == "serious" || a.Level == "2")),
        1 => query.Where(a => a.Level != null && (a.Level.ToLower() == "warning" || a.Level == "1")),
        _ => query.Where(a => a.Level == null ||
            (a.Level.ToLower() != "critical" && a.Level.ToLower() != "serious" && a.Level != "2" &&
             a.Level.ToLower() != "warning" && a.Level != "1"))
    };
}
