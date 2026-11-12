using System.ComponentModel.DataAnnotations;

namespace IotGateway.Application.DTOs;

public sealed record DeviceDto(long Id, string DeviceCode, string DeviceName, string DeviceType,
    string Manufacturer, string Model, int Status, string InstallLocation, DateTime CreatedAt, DateTime UpdatedAt);

public sealed record DevicePageDto(IReadOnlyList<DeviceDto> Items, int Total, int Page, int PageSize);
public sealed record DeviceMetricDto(string Key, string Label, string Value, string Unit,
    bool Warning, bool Critical, byte Quality, DateTime CollectedAt);
public sealed record DeviceRealtimeDto(long Id, string DeviceCode, string DeviceName, string DeviceType,
    int Status, bool HasWarning, bool HasCritical, IReadOnlyList<DeviceMetricDto> Metrics, DateTime? Timestamp);
public sealed record DeviceHistoryDto(long Id, string DataKey, decimal Value, string Unit, byte Quality,
    DateTime Timestamp, DateTime CollectedAt);

public sealed class DeviceQueryRequest : IValidatableObject
{
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;
    [Range(1, 200)]
    public int PageSize { get; set; } = 20;
    [MaxLength(100)]
    public string? Keyword { get; set; }
    [MaxLength(50)]
    public string? DeviceType { get; set; }
    [Range(0, 2)]
    public int? Status { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if ((long)(Page - 1) * PageSize > int.MaxValue)
            yield return new ValidationResult("分页偏移量过大。", new[] { nameof(Page) });
    }
}

public class DeviceWriteRequest : IValidatableObject
{
    [MaxLength(50)]
    public string? DeviceCode { get; set; }
    [MaxLength(100)]
    public string? DeviceName { get; set; }
    [MaxLength(50)]
    public string? DeviceType { get; set; }
    [MaxLength(100)]
    public string? Manufacturer { get; set; }
    [MaxLength(100)]
    public string? Model { get; set; }
    [MaxLength(100)]
    public string? InstallLocation { get; set; }
    [Range(0, 2)]
    public int? Status { get; set; }

    public virtual IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DeviceCode is not null && string.IsNullOrWhiteSpace(DeviceCode))
            yield return new ValidationResult("设备编号不能为空。", new[] { nameof(DeviceCode) });
        if (DeviceName is not null && string.IsNullOrWhiteSpace(DeviceName))
            yield return new ValidationResult("设备名称不能为空。", new[] { nameof(DeviceName) });
        if (DeviceType is not null && string.IsNullOrWhiteSpace(DeviceType))
            yield return new ValidationResult("设备类型不能为空。", new[] { nameof(DeviceType) });
    }
}

public sealed class DeviceCreateRequest : DeviceWriteRequest
{
    public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var error in base.Validate(validationContext))
            yield return error;
        if (DeviceCode is null || DeviceName is null || DeviceType is null)
            yield return new ValidationResult("必须提供 deviceCode、deviceName 和 deviceType。");
    }
}

public sealed class DeviceBatchDeleteRequest : IValidatableObject
{
    [Required, MinLength(1), MaxLength(200)]
    public long[] Ids { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Ids is not null && Ids.Any(id => id <= 0))
            yield return new ValidationResult("设备 ID 必须为正整数。", new[] { nameof(Ids) });
    }
}

public sealed class DeviceHistoryRequest : IValidatableObject
{
    [Required, MaxLength(50)]
    public string DataKey { get; set; } = "";
    [Required]
    public DateTimeOffset? StartTime { get; set; }
    [Required]
    public DateTimeOffset? EndTime { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartTime > EndTime)
            yield return new ValidationResult("startTime 不能晚于 endTime。");
        if (StartTime.HasValue && EndTime.HasValue && EndTime.Value - StartTime.Value > TimeSpan.FromDays(31))
            yield return new ValidationResult("历史查询范围不能超过 31 天。");
    }
}

public class DeviceTelemetryRequest : IValidatableObject
{
    [Required, MaxLength(50)]
    public string DataKey { get; set; } = "";
    [Required, DeviceDecimal]
    public decimal? Value { get; set; }
    [MaxLength(20)]
    public string Unit { get; set; } = "";
    public byte Quality { get; set; } = 192;
    public DateTimeOffset? CollectedAt { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Quality is not (0 or 64 or 192))
            yield return new ValidationResult("quality 只允许 0（BAD）、64（UNCERTAIN）、192（GOOD）。");
        if (CollectedAt.HasValue && (CollectedAt.Value > DateTimeOffset.UtcNow.AddMinutes(5) ||
                                   CollectedAt.Value < DateTimeOffset.UnixEpoch))
            yield return new ValidationResult("collectedAt 必须在 1970 年之后，且不能超过当前时间五分钟。");
    }
}

public sealed class DeviceTelemetryByIdRequest : DeviceTelemetryRequest
{
    [Range(1, long.MaxValue)]
    public long DeviceId { get; set; }
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class DeviceDecimalAttribute : ValidationAttribute
{
    public DeviceDecimalAttribute() : base("数值必须在 decimal(18,6) 范围内，且最多六位小数。") { }
    public override bool IsValid(object? value) => value is null ||
        value is decimal number && number >= -999999999999.999999m &&
        number <= 999999999999.999999m && decimal.Round(number, 6) == number;
}

public sealed record DeviceTelemetryResult(DeviceHistoryDto Reading, IReadOnlyList<AlarmDto> Alarms);
