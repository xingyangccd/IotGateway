// ============================================================
// 文件：IotGateway.Domain/Entities/DeviceData.cs
// 用途：设备遥测数据实体，映射到数据库 device_data 表（高频写入）
// 所属层：Domain（领域层）
// ============================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IotGateway.Domain.Entities;

/// <summary>
/// 设备遥测数据实体（映射到 device_data 表）
/// 存储从 PLC/传感器采集的原始数据，数据量大
/// </summary>
[Table("device_data")]
public class DeviceData
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>设备 ID（外键 → devices.id）</summary>
    [Column("device_id")]
    public long DeviceId { get; set; }

    /// <summary>参数名称（如"温度"、"压力"、"转速"）</summary>
    [Required]
    [MaxLength(50)]
    [Column("param_name")]
    public string ParamName { get; set; } = string.Empty;

    /// <summary>参数值</summary>
    [Column("param_value")]
    public decimal ParamValue { get; set; }

    /// <summary>参数单位（如"°C"、"MPa"、"rpm"）</summary>
    [Required]
    [MaxLength(20)]
    [Column("param_unit")]
    public string ParamUnit { get; set; } = string.Empty;

    /// <summary>数据质量：192 = GOOD, 64 = UNCERTAIN, 0 = BAD（OPC UA 标准）</summary>
    [Column("quality")]
    public byte Quality { get; set; } = 192;

    /// <summary>数据采集时间（设备端时间戳）</summary>
    [Column("collected_at")]
    public DateTime CollectedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
