// ============================================================
// 文件：IotGateway.Domain/Entities/EnergyRecord.cs
// 用途：能源消耗记录实体，映射到数据库 energy_records 表
// 所属层：Domain（领域层）
// ============================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IotGateway.Domain.Entities;

/// <summary>
/// 能源消耗记录实体（映射到 energy_records 表）
/// </summary>
[Table("energy_records")]
public class EnergyRecord
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public long Id { get; set; }

    [Column("device_id")]
    public long DeviceId { get; set; }

    /// <summary>能源类型：Electricity/Water/Gas</summary>
    [MaxLength(20)]
    [Column("energy_type")]
    public string EnergyType { get; set; } = "Electricity";

    /// <summary>消耗量</summary>
    [Column("consumption")]
    public decimal Consumption { get; set; }

    /// <summary>单位（kWh/m³）</summary>
    [MaxLength(20)]
    [Column("unit")]
    public string Unit { get; set; } = "kWh";

    /// <summary>费用（元）</summary>
    [Column("cost")]
    public decimal Cost { get; set; }

    /// <summary>记录时段（按小时）</summary>
    [Column("record_hour")]
    public DateTime RecordHour { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
