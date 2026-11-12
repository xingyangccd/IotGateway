// ============================================================
// 文件：IotGateway.Domain/Entities/MaintenanceRecord.cs
// 用途：维保执行记录实体，映射到数据库 maintenance_records 表
// 所属层：Domain（领域层）
// ============================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IotGateway.Domain.Entities;

/// <summary>
/// 维保执行记录实体（映射到 maintenance_records 表）
/// </summary>
[Table("maintenance_records")]
public class MaintenanceRecord
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>关联维保计划 ID（0 = 非计划维保）</summary>
    [Column("plan_id")]
    public long PlanId { get; set; }

    /// <summary>关联设备 ID</summary>
    [Column("device_id")]
    public long DeviceId { get; set; }

    /// <summary>维保类型：Planned/Emergency</summary>
    [MaxLength(20)]
    [Column("maintenance_type")]
    public string MaintenanceType { get; set; } = "Planned";

    /// <summary>执行人姓名</summary>
    [MaxLength(50)]
    [Column("executor_name")]
    public string? ExecutorName { get; set; }

    /// <summary>完成的项目</summary>
    [Column("items_completed")]
    public string? ItemsCompleted { get; set; }

    /// <summary>使用的备件（JSON 格式）</summary>
    [Column("spare_parts_used")]
    public string? SparePartsUsed { get; set; }

    /// <summary>维保结果：Completed/Partial/Failed</summary>
    [MaxLength(20)]
    [Column("result")]
    public string Result { get; set; } = "Completed";

    /// <summary>维保费用</summary>
    [Column("cost")]
    public decimal Cost { get; set; }

    [Column("remark")]
    public string? Remark { get; set; }

    /// <summary>执行时间</summary>
    [Column("executed_at")]
    public DateTime ExecutedAt { get; set; }

    /// <summary>下次维保到期时间</summary>
    [Column("next_due_at")]
    public DateTime? NextDueAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
