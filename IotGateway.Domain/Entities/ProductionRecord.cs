// ============================================================
// 文件：IotGateway.Domain/Entities/ProductionRecord.cs
// 用途：生产记录实体，映射到数据库 production_records 表
// 所属层：Domain（领域层）
// ============================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IotGateway.Domain.Entities;

/// <summary>
/// 生产记录实体（映射到 production_records 表）
/// 记录每班次的产量数据
/// </summary>
[Table("production_records")]
public class ProductionRecord
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public long Id { get; set; }

    [Column("device_id")]
    public long DeviceId { get; set; }

    /// <summary>产线名称</summary>
    [MaxLength(50)]
    [Column("production_line")]
    public string? ProductionLine { get; set; }

    /// <summary>班次：Day（白班）/ Night（夜班）</summary>
    [MaxLength(20)]
    [Column("shift")]
    public string? Shift { get; set; }

    /// <summary>班次日期</summary>
    [Column("shift_date")]
    public DateTime? ShiftDate { get; set; }

    /// <summary>计划产量</summary>
    [Column("plan_quantity")]
    public int PlanQuantity { get; set; }

    /// <summary>实际产量</summary>
    [Column("actual_quantity")]
    public int ActualQuantity { get; set; }

    /// <summary>不良品数量</summary>
    [Column("defect_quantity")]
    public int DefectQuantity { get; set; }

    /// <summary>运行时长（分钟）</summary>
    [Column("run_duration")]
    public int RunDuration { get; set; }

    /// <summary>停机时长（分钟）</summary>
    [Column("downtime_duration")]
    public int DowntimeDuration { get; set; }

    /// <summary>操作员姓名</summary>
    [MaxLength(50)]
    [Column("operator_name")]
    public string? OperatorName { get; set; }

    [Column("remark")]
    public string? Remark { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
