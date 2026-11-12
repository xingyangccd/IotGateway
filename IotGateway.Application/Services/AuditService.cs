using System.Globalization;
using IotGateway.Application.DTOs;
using IotGateway.Common.Exceptions;
using IotGateway.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace IotGateway.Application.Services;

public sealed class AuditService : IAuditService
{
    private static readonly string[] Actions =
        { "POST", "PUT", "PATCH", "DELETE", "Login", "Create", "Update", "Delete", "Control" };

    // Only known server vocabulary may leave the database. Character filtering alone
    // would still expose legacy passwords/tokens made entirely of letters and digits.
    private static readonly string[] Targets =
    {
        "Auth", "User", "Users", "Role", "Roles", "Permission", "Permissions",
        "Setting", "Settings", "SystemSettings", "Webhook", "Webhooks",
        "Device", "Devices", "Alarm", "Alarms", "AlarmRule", "AlarmRules",
        "Report", "Reports", "Maintenance", "RepairOrders", "Mes", "Audit", "Unknown"
    };

    private readonly IServiceScopeFactory _scopeFactory;

    public AuditService(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task<AuditPageDto> GetLogsAsync(
        AuditLogQueryDto query, CancellationToken cancellationToken = default)
    {
        if (query is null || query.Page < 1 || query.Page > 100_000
            || query.PageSize < 1 || query.PageSize > 100
            || query.UserId is <= 0 || query.TargetId is <= 0
            || (query.FromUtc.HasValue && query.ToUtc.HasValue && query.FromUtc > query.ToUtc))
            throw new BusinessException("审计查询范围无效");

        var action = ValidateFilter(query.ActionType, Actions);
        var target = ValidateFilter(query.TargetType, Targets);
        var result = ValidateFilter(query.Result, new[] { "Success", "Failed" });
        var from = query.FromUtc?.UtcDateTime;
        var to = query.ToUtc?.UtcDateTime;
        var targetId = query.TargetId?.ToString(CultureInfo.InvariantCulture);

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var options = new DbContextOptionsBuilder<AppDbContext>(
                    scope.ServiceProvider.GetRequiredService<DbContextOptions<AppDbContext>>())
                .UseLoggerFactory(NullLoggerFactory.Instance)
                .LogTo(_ => { })
                .EnableSensitiveDataLogging(false)
                .EnableDetailedErrors(false)
                .Options;
            await using var db = new AppDbContext(options);
            var logs = db.AuditLogs.AsNoTracking();
            if (query.UserId.HasValue) logs = logs.Where(x => x.UserId == query.UserId.Value);
            if (action is not null) logs = logs.Where(x => x.ActionType == action);
            if (target is not null) logs = logs.Where(x => x.TargetType == target);
            if (targetId is not null) logs = logs.Where(x => x.TargetId == targetId);
            if (from.HasValue) logs = logs.Where(x => x.CreatedAt >= from.Value);
            if (to.HasValue) logs = logs.Where(x => x.CreatedAt <= to.Value);
            if (result is not null)
            {
                var prefix = result + ":";
                logs = logs.Where(x => x.Result == result || x.Result.StartsWith(prefix));
            }

            var total = await logs.LongCountAsync(cancellationToken);
            var rows = await logs.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
                .Select(x => new
                {
                    x.Id, x.UserId, x.ActionType, x.TargetType, x.TargetId, x.Result, x.CreatedAt
                })
                .ToListAsync(cancellationToken);
            var items = rows.Select(x =>
            {
                var outcome = ReadOutcome(x.Result);
                return new AuditLogDto(
                    x.Id,
                    x.UserId,
                    x.UserId > 0 ? x.UserId.ToString(CultureInfo.InvariantCulture) : "Unknown",
                    Allowlisted(x.ActionType, Actions),
                    Allowlisted(x.TargetType, Targets),
                    NumericId(x.TargetId),
                    outcome.Result,
                    outcome.HttpStatusCode,
                    outcome.ApplicationCode,
                    DateTime.SpecifyKind(x.CreatedAt, DateTimeKind.Utc));
            }).ToArray();
            return new AuditPageDto(items, total, query.Page, query.PageSize);
        }
        catch
        {
            // The global exception filter returns exception messages to clients.
            throw new BusinessException("审计日志暂时不可用", 503);
        }
    }

    private static string? ValidateFilter(string? value, string[] allowed)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (value.Length > 50 || !allowed.Contains(value, StringComparer.Ordinal))
            throw new BusinessException("审计筛选条件无效");
        return value;
    }

    private static string Allowlisted(string? value, string[] allowed) =>
        value is not null && allowed.Contains(value, StringComparer.Ordinal) ? value : "Unknown";

    private static long? NumericId(string? value) =>
        value is { Length: > 0 and <= 100 }
        && long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
        && id > 0 ? id : null;

    private static (string Result, int? HttpStatusCode, int? ApplicationCode) ReadOutcome(string? value)
    {
        if (value is "Success" or "Failed") return (value, null, null);
        if (value is null || value.Length > 20) return ("Unknown", null, null);
        var parts = value.Split(':');
        if (parts.Length is < 2 or > 3 || parts[0] is not ("Success" or "Failed")
            || !TryStatusCode(parts[1], out var http))
            return ("Unknown", null, null);
        int? application = null;
        if (parts.Length == 3)
        {
            if (!TryStatusCode(parts[2], out var code)) return ("Unknown", null, null);
            application = code;
        }
        var expected = http >= 400 || (application.HasValue && application != 200)
            ? "Failed" : "Success";
        return parts[0] == expected ? (expected, http, application) : ("Unknown", null, null);
    }

    private static bool TryStatusCode(string value, out int code) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out code)
        && code is >= 100 and <= 599
        && value == code.ToString(CultureInfo.InvariantCulture);
}
