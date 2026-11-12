using IotGateway.Application.DTOs;
using IotGateway.Application.Services;
using IotGateway.Common.Exceptions;
using IotGateway.Domain.Entities;
using IotGateway.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IotGateway.WebAPI.Services;

internal static class DeviceTelemetryProcessor
{
    public static Task<DeviceTelemetryResult> PersistAsync(AppDbContext db, long id, DeviceTelemetryRequest request,
        CancellationToken cancellationToken) => DeviceTransactions.RunAsync(db, async () =>
    {
        await DeviceTransactions.LockAsync(db, id, cancellationToken);
        var device = await db.Devices.SingleAsync(d => d.Id == id, cancellationToken);
        var now = DateTime.UtcNow;
        var key = request.DataKey.Trim();
        var collectedAt = request.CollectedAt?.UtcDateTime ?? now;
        var lastSample = await db.DeviceData.Where(d => d.DeviceId == id && d.ParamName == key)
            .OrderByDescending(d => d.CollectedAt).ThenByDescending(d => d.Id)
            .Select(d => (DateTime?)d.CollectedAt).FirstOrDefaultAsync(cancellationToken);
        var reading = new DeviceData
        {
            DeviceId = id, ParamName = key, ParamValue = request.Value!.Value,
            ParamUnit = request.Unit?.Trim() ?? "", Quality = request.Quality,
            CollectedAt = collectedAt, CreatedAt = now
        };
        db.DeviceData.Add(reading);
        device.ConnectionStatus = 1;
        device.UpdatedAt = now;
        var alarms = new List<Alarm>();

        // Backfilled and bad-quality readings remain in history, but cannot cause a current alarm.
        if (reading.Quality == 192 && (!lastSample.HasValue || collectedAt >= lastSample.Value))
        {
            var rules = await db.AlarmRules.AsNoTracking().Where(r => r.IsEnabled == 1 &&
                    (r.RuleType == "Threshold" || r.RuleType == null) && r.ParamName == key &&
                    (r.DeviceId == 0 || r.DeviceId == id) &&
                    (r.DeviceType == "" || r.DeviceType == device.Category))
                .OrderBy(r => r.Id).ToListAsync(cancellationToken);
            foreach (var rule in rules.Where(r => AlarmRuleEvaluator.Matches(r, reading.ParamValue)))
            {
                var lastAlarm = await db.Alarms.Where(a => a.DeviceId == id && a.RuleId == rule.Id)
                    .OrderByDescending(a => a.TriggeredAt).Select(a => (DateTime?)a.TriggeredAt)
                    .FirstOrDefaultAsync(cancellationToken);
                if (lastAlarm.HasValue && lastAlarm.Value.AddSeconds(Math.Clamp(rule.CooldownSeconds, 0, 86400)) > now)
                    continue;
                var alarm = new Alarm
                {
                    DeviceId = id, RuleId = rule.Id, Title = rule.Name, ParamName = key,
                    ParamValue = reading.ParamValue, ThresholdValue = rule.ThresholdLower,
                    Level = rule.Level, Message = AlarmRuleEvaluator.RenderMessage(rule, device, reading.ParamValue),
                    Status = "Active", TriggeredAt = now, CreatedAt = now
                };
                db.Alarms.Add(alarm);
                alarms.Add(alarm);
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        return new DeviceTelemetryResult(DeviceApiQueries.ToDto(reading),
            alarms.Select(a => AlarmRuleEvaluator.ToDto(a, device)).ToArray());
    }, cancellationToken);
}
