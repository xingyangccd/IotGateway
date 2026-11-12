// ============================================================
// 文件：IotGateway.Domain/Entities/Alarm.cs
// 用途：告警记录实体，映射到数据库 alarms 表
// 所属层：Domain（领域层）
// ============================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IotGateway.Domain.Entities;

/// <summary>
/// 告警记录实体（映射到 alarms 表）
/// 告警生命周期：Active → Confirmed → Resolved → Closed
/// </summary>
[Table("alarms")]
public class Alarm
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>告警规则 ID（外键 → alarm_rules.id）</summary>
    [Column("rule_id")]
    public long RuleId { get; set; }

    /// <summary>设备 ID（外键 → devices.id）</summary>
    [Column("device_id")]
    public long DeviceId { get; set; }

    [MaxLength(100)]
    [Column("title")]
    public string? Title { get; set; }

    /// <summary>触发告警的参数名称</summary>
    [MaxLength(50)]
    [Column("param_name")]
    public string? ParamName { get; set; }

    /// <summary>触发时的参数值</summary>
    [Column("param_value")]
    public decimal? ParamValue { get; set; }

    /// <summary>触发时的阈值</summary>
    [Column("threshold_value")]
    public decimal? ThresholdValue { get; set; }

    /// <summary>告警级别</summary>
    [MaxLength(10)]
    [Column("level")]
    public string? Level { get; set; }

    /// <summary>告警消息内容</summary>
    [MaxLength(500)]
    [Column("message")]
    public string? Message { get; set; }

    /// <summary>
    /// 告警状态：
    ///   Active    = 活跃（新告警，未处理）
    ///   Confirmed = 已确认（操作员确认收到）
    ///   Resolved  = 已解决（问题已处理）
    ///   Closed    = 已关闭（归档）
    /// </summary>
    [MaxLength(20)]
    [Column("status")]
    public string Status { get; set; } = "Active";

    /// <summary>确认人 ID（外键 → users.id）</summary>
    [Column("confirmed_by")]
    public long? ConfirmedBy { get; set; }

    /// <summary>确认时间</summary>
    [Column("confirmed_at")]
    public DateTime? ConfirmedAt { get; set; }

    /// <summary>解决人 ID</summary>
    [Column("resolved_by")]
    public long? ResolvedBy { get; set; }

    /// <summary>解决时间</summary>
    [Column("resolved_at")]
    public DateTime? ResolvedAt { get; set; }

    /// <summary>解决备注</summary>
    [Column("resolve_remark")]
    public string? ResolveRemark { get; set; }

    /// <summary>AI 分析结果（大模型自动分析）</summary>
    [Column("ai_analysis")]
    public string? AiAnalysis { get; set; }

    /// <summary>告警触发时间</summary>
    [Column("triggered_at")]
    public DateTime TriggeredAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
