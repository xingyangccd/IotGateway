// ============================================================
// 文件：IotGateway.Domain/Entities/DeviceConfig.cs
// 用途：设备通信配置实体，映射到数据库 device_configs 表
//       与 Device 是 1:1 关系（device_id 唯一外键）
// 所属层：Domain（领域层）
// ============================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IotGateway.Domain.Entities;

/// <summary>
/// 设备通信配置实体（映射到 device_configs 表）
/// 存储 Modbus/串口/MQTT 等不同协议的连接参数
/// </summary>
[Table("device_configs")]
public class DeviceConfig
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>关联设备 ID（唯一，外键 → devices.id，ON DELETE CASCADE）</summary>
    [Column("device_id")]
    public long DeviceId { get; set; }

    /// <summary>协议类型（冗余，方便查询）</summary>
    [Required]
    [MaxLength(20)]
    [Column("protocol")]
    public string Protocol { get; set; } = string.Empty;

    // ===== Modbus TCP 配置 =====

    /// <summary>Modbus TCP IP 地址</summary>
    [MaxLength(50)]
    [Column("modbus_ip")]
    public string? ModbusIp { get; set; }

    /// <summary>Modbus TCP 端口（默认 502）</summary>
    [Column("modbus_port")]
    public int ModbusPort { get; set; } = 502;

    /// <summary>Modbus 从站 ID（默认 1）</summary>
    [Column("modbus_slave_id")]
    public byte ModbusSlaveId { get; set; } = 1;

    /// <summary>Modbus 寄存器起始地址</summary>
    [Column("modbus_register_start")]
    public int ModbusRegisterStart { get; set; }

    /// <summary>Modbus 寄存器数量</summary>
    [Column("modbus_register_count")]
    public int ModbusRegisterCount { get; set; } = 10;

    /// <summary>Modbus 功能码（03=读保持寄存器, 04=读输入寄存器）</summary>
    [Column("modbus_function_code")]
    public byte ModbusFunctionCode { get; set; } = 3;

    // ===== 串口配置 =====

    /// <summary>串口名称（如 COM3 或 /dev/ttyUSB0）</summary>
    [MaxLength(20)]
    [Column("serial_port_name")]
    public string? SerialPortName { get; set; }

    /// <summary>串口波特率（默认 9600）</summary>
    [Column("serial_baud_rate")]
    public int SerialBaudRate { get; set; } = 9600;

    /// <summary>串口数据位（默认 8）</summary>
    [Column("serial_data_bits")]
    public int SerialDataBits { get; set; } = 8;

    /// <summary>串口停止位：One/Two</summary>
    [MaxLength(5)]
    [Column("serial_stop_bits")]
    public string SerialStopBits { get; set; } = "One";

    /// <summary>串口校验位：None/Odd/Even</summary>
    [MaxLength(10)]
    [Column("serial_parity")]
    public string SerialParity { get; set; } = "None";

    // ===== MQTT 配置 =====

    /// <summary>MQTT 订阅/发布主题</summary>
    [MaxLength(200)]
    [Column("mqtt_topic")]
    public string? MqttTopic { get; set; }

    /// <summary>MQTT QoS 等级（0/1/2）</summary>
    [Column("mqtt_qos")]
    public byte MqttQos { get; set; } = 1;

    // ===== 采集配置 =====

    /// <summary>数据采集间隔（毫秒，默认 1000ms）</summary>
    [Column("collect_interval")]
    public int CollectInterval { get; set; } = 1000;

    /// <summary>采集策略：Interval=定时采集, OnChange=变化采集, OnDemand=手动采集</summary>
    [Required]
    [MaxLength(20)]
    [Column("collect_strategy")]
    public string CollectStrategy { get; set; } = "Interval";

    /// <summary>采集开关：1 = 启用, 0 = 禁用</summary>
    [Column("collect_enabled")]
    public sbyte CollectEnabled { get; set; } = 1;

    /// <summary>数据模板（JSON 格式，定义 Modbus 寄存器 → 物理参数的映射）</summary>
    [Column("data_template")]
    public string? DataTemplate { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
