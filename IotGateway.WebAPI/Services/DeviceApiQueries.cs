using System.Globalization;
using IotGateway.Application.DTOs;
using IotGateway.Application.Services;
using IotGateway.Domain.Entities;
using IotGateway.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IotGateway.WebAPI.Services;

internal static class DeviceApiQueries
{
    public static DeviceDto ToDto(Device d) => new(d.Id, d.DeviceCode, d.Name, d.Category, d.Manufacturer,
        d.Model, d.ConnectionStatus, d.Location, Utc(d.CreatedAt), Utc(d.UpdatedAt));

    public static DeviceHistoryDto ToDto(DeviceData d) =>
        new(d.Id, d.ParamName, d.ParamValue, d.ParamUnit, d.Quality, Utc(d.CollectedAt), Utc(d.CollectedAt));

    public static DateTime Utc(DateTime date) => DateTime.SpecifyKind(date, DateTimeKind.Utc);

    public static void Apply(DeviceWriteRequest request, Device device)
    {
        if (request.DeviceCode is not null) device.DeviceCode = request.DeviceCode.Trim();
        if (request.DeviceName is not null) device.Name = request.DeviceName.Trim();
        if (request.DeviceType is not null) device.Category = request.DeviceType.Trim();
        if (request.Manufacturer is not null) device.Manufacturer = request.Manufacturer.Trim();
        if (request.Model is not null) device.Model = request.Model.Trim();
        if (request.InstallLocation is not null) device.Location = request.InstallLocation.Trim();
        if (request.Status.HasValue) device.ConnectionStatus = (sbyte)request.Status.Value;
        device.UpdatedAt = DateTime.UtcNow;
    }

    public static async Task<DeviceRealtimeDto[]> RealtimeAsync(AppDbContext db, long? deviceId,
        CancellationToken cancellationToken)
    {
        var devices = await db.Devices.AsNoTracking().Where(d => !deviceId.HasValue || d.Id == deviceId.Value)
            .OrderBy(d => d.Id).ToListAsync(cancellationToken);
        var latest = await db.DeviceData.AsNoTracking()
            .Where(d => (!deviceId.HasValue || d.DeviceId == deviceId.Value) &&
                !db.DeviceData.Any(other => other.DeviceId == d.DeviceId && other.ParamName == d.ParamName &&
                    (other.CollectedAt > d.CollectedAt || other.CollectedAt == d.CollectedAt && other.Id > d.Id)))
            .ToListAsync(cancellationToken);
        var active = await db.Alarms.AsNoTracking()
            .Where(a => (!deviceId.HasValue || a.DeviceId == deviceId.Value) &&
                a.Status.ToLower() != "resolved" && a.Status.ToLower() != "closed")
            .Select(a => new { a.DeviceId, a.ParamName, a.Level }).ToListAsync(cancellationToken);
        var dataLookup = latest.ToLookup(d => d.DeviceId);
        var alarmLookup = active.ToLookup(a => a.DeviceId);
        return devices.Select(device =>
        {
            var alarms = alarmLookup[device.Id].ToArray();
            var metrics = dataLookup[device.Id].OrderBy(d => d.ParamName).Select(d =>
                new DeviceMetricDto(d.ParamName, d.ParamName, d.ParamValue.ToString(CultureInfo.InvariantCulture),
                    d.ParamUnit, alarms.Any(a => a.ParamName == d.ParamName && AlarmRuleEvaluator.LevelNumber(a.Level) == 1),
                    alarms.Any(a => a.ParamName == d.ParamName && AlarmRuleEvaluator.LevelNumber(a.Level) == 2),
                    d.Quality, Utc(d.CollectedAt))).ToArray();
            return new DeviceRealtimeDto(device.Id, device.DeviceCode, device.Name, device.Category, device.ConnectionStatus,
                alarms.Any(a => AlarmRuleEvaluator.LevelNumber(a.Level) == 1),
                alarms.Any(a => AlarmRuleEvaluator.LevelNumber(a.Level) == 2), metrics,
                metrics.Length == 0 ? null : metrics.Max(m => m.CollectedAt));
        }).ToArray();
    }
}
