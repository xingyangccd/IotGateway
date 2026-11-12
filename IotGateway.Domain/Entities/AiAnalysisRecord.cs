// ============================================================
// 文件：IotGateway.Domain/Entities/AiAnalysisRecord.cs
// 用途：AI 分析记录实体，映射到数据库 ai_analysis_records 表
// 所属层：Domain（领域层）
// ============================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IotGateway.Domain.Entities;

/// <summary>
/// AI 分析记录实体（映射到 ai_analysis_records 表）
/// 记录每次 AI 大模型分析任务的输入、输出、费用
/// </summary>
[Table("ai_analysis_records")]
public class AiAnalysisRecord
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>任务类型：DeviceHealth/Efficiency/AlarmRootCause</summary>
    [MaxLength(50)]
    [Column("task_type")]
    public string? TaskType { get; set; }

    /// <summary>分析目标 ID</summary>
    [MaxLength(100)]
    [Column("target_id")]
    public string? TargetId { get; set; }

    /// <summary>分析目标名称</summary>
    [MaxLength(100)]
    [Column("target_name")]
    public string? TargetName { get; set; }

    /// <summary>输入数据（JSON）</summary>
    [Column("input_data")]
    public string? InputData { get; set; }

    /// <summary>输出结果（JSON）</summary>
    [Column("output_result")]
    public string? OutputResult { get; set; }

    /// <summary>使用的模型名称（如 GPT-4, DeepSeek-V3）</summary>
    [MaxLength(100)]
    [Column("model_used")]
    public string? ModelUsed { get; set; }

    /// <summary>消耗 Token 数</summary>
    [Column("tokens_used")]
    public int TokensUsed { get; set; }

    /// <summary>API 调用费用</summary>
    [Column("cost")]
    public decimal Cost { get; set; }

    /// <summary>分析耗时（毫秒）</summary>
    [Column("duration_ms")]
    public int DurationMs { get; set; }

    /// <summary>状态：0 = 失败, 1 = 成功</summary>
    [Column("status")]
    public sbyte Status { get; set; } = 1;

    /// <summary>错误消息（失败时记录）</summary>
    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    /// <summary>触发类型：Scheduled/Event/Manual</summary>
    [MaxLength(20)]
    [Column("trigger_type")]
    public string TriggerType { get; set; } = "Scheduled";

    /// <summary>触发时间</summary>
    [Column("triggered_at")]
    public DateTime TriggeredAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
