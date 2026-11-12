using System.ComponentModel.DataAnnotations;

namespace IotGateway.Application.DTOs;

public sealed class AlarmDto
{
    public long Id { get; init; }
    public string DeviceCode { get; init; } = string.Empty;
    public string DeviceName { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public int Level { get; init; }
    public string LevelText { get; init; } = string.Empty;
    public string Status { get; init; } = "unconfirmed";
    public string Content { get; init; } = string.Empty;
    public string? ConfirmedBy { get; init; }
    public DateTime? ConfirmedAt { get; init; }
    public string? ResolvedBy { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public string? Remark { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed record AlarmPageDto(IReadOnlyList<AlarmDto> Items, int Total, int Page, int PageSize);
public sealed record AlarmStatsDto(int TodayTotal, int Unconfirmed, int SeriousCount, int[] WeeklyTrend);

public sealed class AlarmQueryDto : IValidatableObject
{
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 200)]
    public int PageSize { get; set; } = 20;

    [Range(0, 2)]
    public int? Level { get; set; }

    [RegularExpression("^(unconfirmed|confirmed|resolved)$")]
    public string? Status { get; set; }

    [MaxLength(50)]
    public string? DeviceCode { get; set; }

    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartDate.HasValue && EndDate.HasValue && StartDate > EndDate)
            yield return new ValidationResult("startDate 不能晚于 endDate。", new[] { nameof(EndDate) });
        if ((long)(Page - 1) * PageSize > int.MaxValue)
            yield return new ValidationResult("分页偏移量过大。", new[] { nameof(Page) });
    }
}

public sealed class ResolveAlarmRequest
{
    [Required(AllowEmptyStrings = true), MaxLength(2000)]
    public string? Remark { get; set; }
}

public sealed class ToggleAlarmRuleRequest
{
    [Required]
    public bool? Enabled { get; set; }
}

public sealed class AlarmRuleDto
{
    public long Id { get; init; }
    public string DeviceType { get; init; } = string.Empty;
    public string DataKey { get; init; } = string.Empty;
    public string DataKeyLabel { get; init; } = string.Empty;
    public string RuleName { get; init; } = string.Empty;
    public string ComparisonType { get; init; } = "gt";
    public decimal? Threshold { get; init; }
    public decimal? ThresholdMax { get; init; }
    public int AlarmLevel { get; init; }
    public string AlarmTemplate { get; init; } = string.Empty;
    public bool Enabled { get; init; }
    public int CooldownSeconds { get; init; }
}

public class AlarmRuleRequest : IValidatableObject
{
    [Required, MaxLength(50)]
    public string DeviceType { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string DataKey { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? DataKeyLabel { get; set; }

    [Required, MaxLength(100)]
    public string RuleName { get; set; } = string.Empty;

    [Required, MaxLength(10), RegularExpression("^(gt|lt|eq|ne|gte|lte|between)$")]
    public string ComparisonType { get; set; } = "gt";

    [Required, AlarmDecimal]
    public decimal? Threshold { get; set; }

    [AlarmDecimal]
    public decimal? ThresholdMax { get; set; }

    [Required, Range(0, 2)]
    public int? AlarmLevel { get; set; }

    [MaxLength(500)]
    public string? AlarmTemplate { get; set; }

    public bool Enabled { get; set; } = true;

    [Range(0, 86400)]
    public int CooldownSeconds { get; set; } = 300;

    public virtual IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ComparisonType == "between" &&
            (!ThresholdMax.HasValue || !Threshold.HasValue || Threshold >= ThresholdMax))
            yield return new ValidationResult("between 必须提供 thresholdMax，且 threshold 小于 thresholdMax。",
                new[] { nameof(ThresholdMax) });
    }
}

public sealed class AlarmRuleTestRequest : AlarmRuleRequest
{
    [AlarmDecimal]
    public decimal? Value { get; set; }

    [AlarmDecimal]
    public decimal? TestValue { get; set; }

    public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var error in base.Validate(validationContext))
            yield return error;
        if (Value.HasValue && TestValue.HasValue && Value != TestValue)
            yield return new ValidationResult("value 与 testValue 同时提供时必须相同。",
                new[] { nameof(TestValue) });
    }
}

public sealed record AlarmRuleTestResult(bool Matched, string Message);

[AttributeUsage(AttributeTargets.Property)]
public sealed class AlarmDecimalAttribute : ValidationAttribute
{
    public AlarmDecimalAttribute() : base("数值必须在 decimal(18,6) 范围内，且最多六位小数。") { }

    public override bool IsValid(object? value) => value is null ||
        value is decimal number && number >= -999999999999.999999m &&
        number <= 999999999999.999999m && decimal.Round(number, 6) == number;
}
