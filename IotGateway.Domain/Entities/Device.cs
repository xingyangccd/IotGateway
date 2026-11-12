// ============================================================
// 文件：IotGateway.Domain/Entities/Device.cs
// 用途：设备资产实体，映射到数据库 devices 表
// 所属层：Domain（领域层）
// ============================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IotGateway.Domain.Entities;

/// <summary>
/// 设备资产实体（映射到 devices 表）
/// 记录每台设备的资产信息、位置、运行状态
/// </summary>
[Table("devices")]
public class Device
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>设备编号（唯一标识，如 EQ-2024-001）</summary>
    [Required]
    [MaxLength(50)]
    [Column("device_code")]
    public string DeviceCode { get; set; } = string.Empty;

    /// <summary>设备名称（如 1号注塑机）</summary>
    [Required]
    [MaxLength(100)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>设备型号（如 MA1600/540）</summary>
    [Required]
    [MaxLength(100)]
    [Column("model")]
    public string Model { get; set; } = string.Empty;

    /// <summary>设备类别：PLC/Sensor/Motor/Instrument/Other</summary>
    [Required]
    [MaxLength(50)]
    [Column("category")]
    public string Category { get; set; } = string.Empty;

    /// <summary>制造商</summary>
    [Required]
    [MaxLength(100)]
    [Column("manufacturer")]
    public string Manufacturer { get; set; } = string.Empty;

    /// <summary>供应商</summary>
    [Required]
    [MaxLength(100)]
    [Column("supplier")]
    public string Supplier { get; set; } = string.Empty;

    /// <summary>采购日期</summary>
    [Column("purchase_date")]
    public DateTime? PurchaseDate { get; set; }

    /// <summary>保修到期日期</summary>
    [Column("warranty_expire")]
    public DateTime? WarrantyExpire { get; set; }

    /// <summary>所属车间</summary>
    [Required]
    [MaxLength(50)]
    [Column("workshop")]
    public string Workshop { get; set; } = string.Empty;

    /// <summary>所属产线</summary>
    [Required]
    [MaxLength(50)]
    [Column("production_line")]
    public string ProductionLine { get; set; } = string.Empty;

    /// <summary>安装位置</summary>
    [Required]
    [MaxLength(100)]
    [Column("location")]
    public string Location { get; set; } = string.Empty;

    /// <summary>通信协议：ModbusTCP/ModbusRTU/SerialPort/MQTT/OPCUA</summary>
    [Required]
    [MaxLength(20)]
    [Column("protocol")]
    public string Protocol { get; set; } = string.Empty;

    /// <summary>连接状态：0 = 离线, 1 = 在线, 2 = 故障</summary>
    [Column("connection_status")]
    public sbyte ConnectionStatus { get; set; }

    /// <summary>运行状态：0 = 停止, 1 = 运行, 2 = 待机, 3 = 维修, 4 = 报废</summary>
    [Column("run_status")]
    public sbyte RunStatus { get; set; }

    /// <summary>累计运行小时数</summary>
    [Column("total_run_hours")]
    public decimal TotalRunHours { get; set; }

    /// <summary>设备图片 URL</summary>
    [MaxLength(500)]
    [Column("image_url")]
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>设备二维码</summary>
    [MaxLength(500)]
    [Column("qr_code")]
    public string QrCode { get; set; } = string.Empty;

    /// <summary>备注</summary>
    [Column("remark")]
    public string? Remark { get; set; }

    /// <summary>创建人 ID（外键 → users.id）</summary>
    [Column("created_by")]
    public long CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
