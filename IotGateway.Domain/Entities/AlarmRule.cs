// ============================================================
// 文件：IotGateway.Domain/Entities/AlarmRule.cs
// 用途：告警规则实体，映射到数据库 alarm_rules 表
// 所属层：Domain（领域层）
// ============================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IotGateway.Domain.Entities;

/// <summary>
/// 告警规则实体（映射到 alarm_rules 表）
/// 定义触发告警的条件：阈值超限、变化率过大、通信中断等
/// </summary>
[Table("alarm_rules")]
public class AlarmRule
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>规则名称</summary>
    [Required]
    [MaxLength(100)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>设备 ID（0 = 全局规则）</summary>
    [Column("device_id")]
    public long DeviceId { get; set; }

    [MaxLength(50)]
    [Column("device_type")]
    public string DeviceType { get; set; } = string.Empty;

    [MaxLength(10)]
    [Column("comparison_type")]
    public string ComparisonType { get; set; } = "gt";

    [MaxLength(50)]
    [Column("data_key_label")]
    public string DataKeyLabel { get; set; } = string.Empty;

    /// <summary>监控参数名称</summary>
    [MaxLength(50)]
    [Column("param_name")]
    public string? ParamName { get; set; }

    /// <summary>规则类型：Threshold/ChangeRate/CommLost/Offline</summary>
    [MaxLength(30)]
    [Column("rule_type")]
    public string? RuleType { get; set; }

    /// <summary>区间上限，对应 API thresholdMax</summary>
    [Column("threshold_upper")]
    public decimal? ThresholdUpper { get; set; }

    /// <summary>比较阈值或区间下限，对应 API threshold</summary>
    [Column("threshold_lower")]
    public decimal? ThresholdLower { get; set; }

    /// <summary>变化率阈值（百分比）</summary>
    [Column("change_rate")]
    public decimal? ChangeRate { get; set; }

    /// <summary>持续时间（秒），超过才触发告警</summary>
    [Column("duration_seconds")]
    public int DurationSeconds { get; set; }

    /// <summary>告警级别：Info/Warning/Critical</summary>
    [MaxLength(10)]
    [Column("level")]
    public string Level { get; set; } = "Warning";

    /// <summary>是否启用：1 = 启用, 0 = 禁用</summary>
    [Column("is_enabled")]
    public sbyte IsEnabled { get; set; } = 1;

    /// <summary>冷却时间（秒），告警恢复后多久可再次触发</summary>
    [Column("cooldown_seconds")]
    public int CooldownSeconds { get; set; } = 300;

    /// <summary>告警消息模板</summary>
    [MaxLength(500)]
    [Column("message_template")]
    public string? MessageTemplate { get; set; }

    /// <summary>创建人 ID</summary>
    [Column("created_by")]
    public long CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
