// ============================================================
// 文件：IotGateway.Domain/Entities/RepairOrder.cs
// 用途：维修工单实体，映射到数据库 repair_orders 表
// 所属层：Domain（领域层）
// ============================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IotGateway.Domain.Entities;

/// <summary>
/// 维修工单实体（映射到 repair_orders 表）
/// 工单生命周期：Pending → Assigned → Repairing → Completed → Verified
/// </summary>
[Table("repair_orders")]
public class RepairOrder
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>工单编号（唯一，如"RO-2024-001"）</summary>
    [Required]
    [MaxLength(30)]
    [Column("order_no")]
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>关联设备 ID</summary>
    [Column("device_id")]
    public long DeviceId { get; set; }

    /// <summary>故障现象描述</summary>
    [Column("fault_phenomenon")]
    public string? FaultPhenomenon { get; set; }

    /// <summary>故障原因分析</summary>
    [Column("fault_cause")]
    public string? FaultCause { get; set; }

    /// <summary>维修措施</summary>
    [Column("repair_action")]
    public string? RepairAction { get; set; }

    /// <summary>
    /// 工单状态：
    ///   Pending    = 待处理
    ///   Assigned   = 已指派
    ///   Repairing  = 维修中
    ///   Completed  = 已修复
    ///   Verified   = 已验证
    /// </summary>
    [MaxLength(20)]
    [Column("status")]
    public string Status { get; set; } = "Pending";

    /// <summary>紧急程度：Normal/Urgent/Critical</summary>
    [MaxLength(10)]
    [Column("severity")]
    public string Severity { get; set; } = "Normal";

    /// <summary>报修人 ID</summary>
    [Column("reporter_id")]
    public long ReporterId { get; set; }

    /// <summary>指派人 ID</summary>
    [Column("assigned_to")]
    public long? AssignedTo { get; set; }

    [Column("assigned_at")]
    public DateTime? AssignedAt { get; set; }

    [Column("started_at")]
    public DateTime? StartedAt { get; set; }

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    /// <summary>验证人 ID</summary>
    [Column("verified_by")]
    public long? VerifiedBy { get; set; }

    [Column("verified_at")]
    public DateTime? VerifiedAt { get; set; }

    /// <summary>维修费用</summary>
    [Column("repair_cost")]
    public decimal RepairCost { get; set; }

    /// <summary>停机时间（分钟）</summary>
    [Column("downtime_minutes")]
    public int DowntimeMinutes { get; set; }

    /// <summary>使用的备件（JSON）</summary>
    [Column("spare_parts_used")]
    public string? SparePartsUsed { get; set; }

    [Column("remark")]
    public string? Remark { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
