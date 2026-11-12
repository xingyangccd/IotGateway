// ============================================================
// 文件：IotGateway.Domain/Entities/MaintenancePlan.cs
// 用途：维保计划实体，映射到数据库 maintenance_plans 表
// 所属层：Domain（领域层）
// ============================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IotGateway.Domain.Entities;

/// <summary>
/// 预防性维保计划实体（映射到 maintenance_plans 表）
/// </summary>
[Table("maintenance_plans")]
public class MaintenancePlan
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>关联设备 ID</summary>
    [Column("device_id")]
    public long DeviceId { get; set; }

    /// <summary>计划名称（如"注塑机月度保养"）</summary>
    [MaxLength(100)]
    [Column("plan_name")]
    public string? PlanName { get; set; }

    /// <summary>周期类型：RunHours/Calendar</summary>
    [MaxLength(20)]
    [Column("cycle_type")]
    public string? CycleType { get; set; }

    /// <summary>周期值（如 720 小时 或 30 天）</summary>
    [Column("cycle_value")]
    public int CycleValue { get; set; }

    /// <summary>维保项目（JSON 数组）</summary>
    [Column("items")]
    public string? Items { get; set; }

    /// <summary>预计需要时间（分钟）</summary>
    [Column("estimated_duration")]
    public int EstimatedDuration { get; set; } = 60;

    /// <summary>所需备件（JSON 格式）</summary>
    [Column("required_spare_parts")]
    public string? RequiredSpareParts { get; set; }

    /// <summary>是否启用</summary>
    [Column("is_enabled")]
    public sbyte IsEnabled { get; set; } = 1;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
