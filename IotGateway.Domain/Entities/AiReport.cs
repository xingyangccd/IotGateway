// ============================================================
// 文件：IotGateway.Domain/Entities/AiReport.cs
// 用途：AI 报告实体，映射到数据库 ai_reports 表
// 所属层：Domain（领域层）
// ============================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IotGateway.Domain.Entities;

/// <summary>
/// AI 生成报告实体（映射到 ai_reports 表）
/// 存储 AI 大模型生成的日报/周报/月报
/// </summary>
[Table("ai_reports")]
public class AiReport
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>报告类型：Daily/Weekly/Monthly</summary>
    [MaxLength(20)]
    [Column("report_type")]
    public string? ReportType { get; set; }

    /// <summary>报告日期</summary>
    [Column("report_date")]
    public DateTime? ReportDate { get; set; }

    /// <summary>报告标题</summary>
    [MaxLength(200)]
    [Column("title")]
    public string? Title { get; set; }

    /// <summary>报告内容（Markdown 格式）</summary>
    [Column("content")]
    public string? Content { get; set; }

    /// <summary>报告摘要</summary>
    [MaxLength(500)]
    [Column("summary")]
    public string? Summary { get; set; }

    /// <summary>关键指标（JSON）</summary>
    [Column("key_metrics")]
    public string? KeyMetrics { get; set; }

    /// <summary>使用的 AI 模型</summary>
    [MaxLength(100)]
    [Column("model_used")]
    public string? ModelUsed { get; set; }

    /// <summary>消耗 Token 数</summary>
    [Column("tokens_used")]
    public int TokensUsed { get; set; }

    /// <summary>生成耗时（毫秒）</summary>
    [Column("generation_time_ms")]
    public int GenerationTimeMs { get; set; }

    [Column("generated_at")]
    public DateTime GeneratedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
