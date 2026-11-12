// ============================================================
// 文件：IotGateway.Domain/Entities/Role.cs
// 用途：RBAC 角色实体，映射到数据库 roles 表
//       对应 sql/02-rbac-tables.sql（第四章新增）
// 所属层：Domain（领域层）
// ============================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IotGateway.Domain.Entities;

/// <summary>
/// 角色实体（映射到 roles 表）
/// 如：Admin（管理员）、Engineer（工程师）、Operator（操作员）
/// </summary>
[Table("roles")]
public class Role
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>角色名称（如"系统管理员"）</summary>
    [Required]
    [MaxLength(50)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>角色编码（唯一，如"admin"、"engineer"）</summary>
    [Required]
    [MaxLength(50)]
    [Column("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>角色描述</summary>
    [MaxLength(200)]
    [Column("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>状态：1 = 启用, 0 = 禁用</summary>
    [Column("status")]
    public sbyte Status { get; set; } = 1;

    /// <summary>排序序号（数值越小越靠前）</summary>
    [Column("sort_order")]
    public int SortOrder { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
