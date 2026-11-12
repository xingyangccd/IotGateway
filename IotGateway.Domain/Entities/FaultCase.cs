// ============================================================
// 文件：IotGateway.Domain/Entities/FaultCase.cs
// 用途：故障案例实体（RAG 知识库），映射到数据库 fault_cases 表
// 所属层：Domain（领域层）
// ============================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IotGateway.Domain.Entities;

/// <summary>
/// 故障案例实体（映射到 fault_cases 表）
/// 用于 AI 大模型 RAG（检索增强生成）知识库
/// fault_phenomenon + fault_cause + repair_steps 有 FULLTEXT 全文索引
/// </summary>
[Table("fault_cases")]
public class FaultCase
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>设备型号</summary>
    [MaxLength(100)]
    [Column("device_model")]
    public string? DeviceModel { get; set; }

    /// <summary>设备类别</summary>
    [MaxLength(50)]
    [Column("device_category")]
    public string? DeviceCategory { get; set; }

    /// <summary>故障现象</summary>
    [Column("fault_phenomenon")]
    public string? FaultPhenomenon { get; set; }

    /// <summary>故障原因</summary>
    [Column("fault_cause")]
    public string? FaultCause { get; set; }

    /// <summary>维修步骤</summary>
    [Column("repair_steps")]
    public string? RepairSteps { get; set; }

    /// <summary>预防措施</summary>
    [Column("preventive_measure")]
    public string? PreventiveMeasure { get; set; }

    /// <summary>所需工具</summary>
    [MaxLength(500)]
    [Column("tools_required")]
    public string? ToolsRequired { get; set; }

    /// <summary>维修难度：Easy/Medium/Hard</summary>
    [MaxLength(10)]
    [Column("difficulty")]
    public string Difficulty { get; set; } = "Medium";

    /// <summary>预计维修时间（分钟）</summary>
    [Column("estimated_time")]
    public int EstimatedTime { get; set; } = 60;

    /// <summary>关联维修工单 ID</summary>
    [Column("related_repair_id")]
    public long? RelatedRepairId { get; set; }

    /// <summary>标签（逗号分隔，用于分类检索）</summary>
    [MaxLength(500)]
    [Column("tags")]
    public string? Tags { get; set; }

    /// <summary>文本嵌入向量（JSON 格式，用于向量语义搜索）</summary>
    [Column("embedding_vector")]
    public string? EmbeddingVector { get; set; }

    /// <summary>创建人 ID</summary>
    [Column("created_by")]
    public long CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
