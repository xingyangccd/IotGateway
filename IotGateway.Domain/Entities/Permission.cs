// ============================================================
// 文件：IotGateway.Domain/Entities/Permission.cs
// 用途：RBAC 权限实体，映射到数据库 permissions 表
// 所属层：Domain（领域层）
// ============================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IotGateway.Domain.Entities;

/// <summary>
/// 权限实体（映射到 permissions 表）
/// 每个权限定义了一个具体的操作能力
/// 如：device:read（设备查看）、alarm:confirm（告警确认）
/// </summary>
[Table("permissions")]
public class Permission
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>权限名称（显示用）</summary>
    [Required]
    [MaxLength(100)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>权限编码（唯一标识，如"device:read"）</summary>
    [Required]
    [MaxLength(100)]
    [Column("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>资源名称（如"device"、"alarm"）</summary>
    [MaxLength(50)]
    [Column("resource")]
    public string Resource { get; set; } = string.Empty;

    /// <summary>操作类型（如"read"、"write"、"delete"）</summary>
    [MaxLength(50)]
    [Column("action")]
    public string Action { get; set; } = string.Empty;

    /// <summary>权限描述</summary>
    [MaxLength(200)]
    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
