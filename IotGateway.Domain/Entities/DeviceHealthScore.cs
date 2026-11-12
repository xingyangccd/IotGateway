// ============================================================
// 文件：IotGateway.Domain/Entities/DeviceHealthScore.cs
// 用途：设备健康评分实体，映射到数据库 device_health_scores 表
// 所属层：Domain（领域层）
// ============================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IotGateway.Domain.Entities;

/// <summary>
/// 设备健康评分实体（映射到 device_health_scores 表）
/// AI 对设备的综合健康评估
/// </summary>
[Table("device_health_scores")]
public class DeviceHealthScore
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>设备 ID（外键 → devices.id）</summary>
    [Column("device_id")]
    public long DeviceId { get; set; }

    /// <summary>健康评分（0-100，越高越健康）</summary>
    [Column("score")]
    public int Score { get; set; }

    /// <summary>趋势：stable/improving/degrading/critical</summary>
    [MaxLength(20)]
    [Column("trend")]
    public string Trend { get; set; } = "stable";

    /// <summary>异常检测结果（JSON）</summary>
    [Column("anomalies")]
    public string? Anomalies { get; set; }

    /// <summary>AI 建议</summary>
    [Column("recommendation")]
    public string? Recommendation { get; set; }

    /// <summary>关联 AI 分析记录 ID</summary>
    [Column("analysis_record_id")]
    public long? AnalysisRecordId { get; set; }

    /// <summary>评估时间</summary>
    [Column("evaluated_at")]
    public DateTime EvaluatedAt { get; set; }
}
