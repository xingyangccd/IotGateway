// ============================================================
// 文件：IotGateway.Domain/Entities/SparePartRecord.cs
// 用途：备件出入库记录实体，映射到数据库 spare_part_records 表
// 所属层：Domain（领域层）
// ============================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IotGateway.Domain.Entities;

/// <summary>
/// 备件出入库记录实体（映射到 spare_part_records 表）
/// </summary>
[Table("spare_part_records")]
public class SparePartRecord
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>备件 ID（外键 → spare_parts.id）</summary>
    [Column("part_id")]
    public long PartId { get; set; }

    /// <summary>变更类型：In(入库) / Out(出库)</summary>
    [MaxLength(10)]
    [Column("change_type")]
    public string? ChangeType { get; set; }

    /// <summary>变更数量</summary>
    [Column("quantity")]
    public int Quantity { get; set; }

    /// <summary>变更前库存</summary>
    [Column("before_stock")]
    public int BeforeStock { get; set; }

    /// <summary>变更后库存</summary>
    [Column("after_stock")]
    public int AfterStock { get; set; }

    /// <summary>关联工单编号</summary>
    [MaxLength(50)]
    [Column("related_order_no")]
    public string? RelatedOrderNo { get; set; }

    /// <summary>操作员 ID</summary>
    [Column("operator_id")]
    public long OperatorId { get; set; }

    /// <summary>备注</summary>
    [MaxLength(500)]
    [Column("remark")]
    public string? Remark { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
