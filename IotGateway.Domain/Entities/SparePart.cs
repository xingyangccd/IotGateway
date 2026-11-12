// ============================================================
// 文件：IotGateway.Domain/Entities/SparePart.cs
// 用途：备件库存实体，映射到数据库 spare_parts 表
// 所属层：Domain（领域层）
// ============================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IotGateway.Domain.Entities;

/// <summary>
/// 备件库存实体（映射到 spare_parts 表）
/// </summary>
[Table("spare_parts")]
public class SparePart
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>备件编码（唯一）</summary>
    [Required]
    [MaxLength(50)]
    [Column("part_code")]
    public string PartCode { get; set; } = string.Empty;

    /// <summary>备件名称</summary>
    [MaxLength(100)]
    [Column("name")]
    public string? Name { get; set; }

    /// <summary>规格型号</summary>
    [MaxLength(100)]
    [Column("model")]
    public string? Model { get; set; }

    /// <summary>备件类别</summary>
    [MaxLength(50)]
    [Column("category")]
    public string? Category { get; set; }

    /// <summary>计量单位</summary>
    [MaxLength(20)]
    [Column("unit")]
    public string Unit { get; set; } = "个";

    /// <summary>当前库存数量</summary>
    [Column("current_stock")]
    public int CurrentStock { get; set; }

    /// <summary>最小库存（低于此值触发预警）</summary>
    [Column("min_stock")]
    public int MinStock { get; set; } = 5;

    /// <summary>最大库存</summary>
    [Column("max_stock")]
    public int MaxStock { get; set; } = 100;

    /// <summary>单价</summary>
    [Column("unit_price")]
    public decimal UnitPrice { get; set; }

    /// <summary>货架位置</summary>
    [MaxLength(50)]
    [Column("shelf_location")]
    public string? ShelfLocation { get; set; }

    /// <summary>供应商</summary>
    [MaxLength(100)]
    [Column("supplier")]
    public string? Supplier { get; set; }

    /// <summary>供应商联系方式</summary>
    [MaxLength(50)]
    [Column("supplier_contact")]
    public string? SupplierContact { get; set; }

    [Column("remark")]
    public string? Remark { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
