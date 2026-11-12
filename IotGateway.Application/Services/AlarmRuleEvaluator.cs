using System.Globalization;
using IotGateway.Application.DTOs;
using IotGateway.Domain.Entities;

namespace IotGateway.Application.Services;

public static class AlarmRuleEvaluator
{
    public static bool Matches(AlarmRule rule, decimal value)
    {
        if (rule.ThresholdLower is not decimal threshold)
            return false;

        return rule.ComparisonType?.ToLowerInvariant() switch
        {
            "gt" => value > threshold,
            "lt" => value < threshold,
            "eq" => value == threshold,
            "ne" => value != threshold,
            "gte" => value >= threshold,
            "lte" => value <= threshold,
            // Vue's expression preview says “介于”: both range endpoints are included.
            "between" => rule.ThresholdUpper is decimal upper && threshold < upper &&
                         value >= threshold && value <= upper,
            _ => false
        };
    }

    public static int LevelNumber(string? level) => level?.Trim().ToLowerInvariant() switch
    {
        "critical" or "serious" or "2" => 2,
        "warning" or "1" => 1,
        _ => 0
    };

    public static string LevelName(int level) => level switch
    {
        0 => "Info",
        1 => "Warning",
        2 => "Critical",
        _ => throw new ArgumentOutOfRangeException(nameof(level))
    };

    public static string RenderMessage(AlarmRule rule, Device device, decimal value)
    {
        var template = string.IsNullOrWhiteSpace(rule.MessageTemplate)
            ? "{deviceCode} 的 {dataKeyLabel} 达到 {value}，触发告警：{ruleName}"
            : rule.MessageTemplate;
        var message = template
            .Replace("{deviceCode}", device.DeviceCode, StringComparison.Ordinal)
            .Replace("{deviceName}", device.Name, StringComparison.Ordinal)
            .Replace("{deviceType}", rule.DeviceType, StringComparison.Ordinal)
            .Replace("{dataKeyLabel}", string.IsNullOrWhiteSpace(rule.DataKeyLabel)
                ? rule.ParamName : rule.DataKeyLabel, StringComparison.Ordinal)
            .Replace("{dataKey}", rule.ParamName, StringComparison.Ordinal)
            .Replace("{value}", value.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{threshold}", rule.ThresholdLower?.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{thresholdMax}", rule.ThresholdUpper?.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{ruleName}", rule.Name, StringComparison.Ordinal);
        return message.Length <= 500 ? message : message[..500];
    }

    public static void Apply(AlarmRuleRequest request, AlarmRule rule)
    {
        rule.Name = request.RuleName.Trim();
        rule.DeviceType = request.DeviceType.Trim();
        rule.ParamName = request.DataKey.Trim();
        rule.DataKeyLabel = string.IsNullOrWhiteSpace(request.DataKeyLabel)
            ? rule.ParamName : request.DataKeyLabel.Trim();
        rule.ComparisonType = request.ComparisonType;
        rule.ThresholdLower = request.Threshold;
        rule.ThresholdUpper = request.ThresholdMax;
        rule.RuleType = "Threshold";
        rule.Level = LevelName(request.AlarmLevel!.Value);
        rule.MessageTemplate = request.AlarmTemplate;
        rule.IsEnabled = request.Enabled ? (sbyte)1 : (sbyte)0;
        rule.CooldownSeconds = request.CooldownSeconds;
    }

    public static AlarmRuleDto ToDto(AlarmRule rule) => new()
    {
        Id = rule.Id,
        DeviceType = rule.DeviceType,
        DataKey = rule.ParamName ?? string.Empty,
        DataKeyLabel = rule.DataKeyLabel,
        RuleName = rule.Name,
        ComparisonType = rule.ComparisonType,
        Threshold = rule.ThresholdLower,
        ThresholdMax = rule.ThresholdUpper,
        AlarmLevel = LevelNumber(rule.Level),
        AlarmTemplate = rule.MessageTemplate ?? string.Empty,
        Enabled = rule.IsEnabled == 1,
        CooldownSeconds = rule.CooldownSeconds
    };

    public static AlarmDto ToDto(Alarm alarm, Device? device,
        string? confirmedBy = null, string? resolvedBy = null) => new()
    {
        Id = alarm.Id,
        DeviceCode = device?.DeviceCode ?? string.Empty,
        DeviceName = device?.Name ?? string.Empty,
        Title = alarm.Title ?? alarm.ParamName ?? "设备告警",
        Level = LevelNumber(alarm.Level),
        LevelText = LevelNumber(alarm.Level) switch { 2 => "严重", 1 => "警告", _ => "提示" },
        Status = alarm.Status.ToLowerInvariant() switch
        {
            "confirmed" => "confirmed",
            "resolved" or "closed" => "resolved",
            _ => "unconfirmed"
        },
        Content = alarm.Message ?? string.Empty,
        ConfirmedBy = confirmedBy ?? alarm.ConfirmedBy?.ToString(CultureInfo.InvariantCulture),
        ConfirmedAt = Utc(alarm.ConfirmedAt),
        ResolvedBy = resolvedBy ?? alarm.ResolvedBy?.ToString(CultureInfo.InvariantCulture),
        ResolvedAt = Utc(alarm.ResolvedAt),
        Remark = alarm.ResolveRemark,
        CreatedAt = DateTime.SpecifyKind(alarm.CreatedAt, DateTimeKind.Utc)
    };

    private static DateTime? Utc(DateTime? value) =>
        value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;
}
