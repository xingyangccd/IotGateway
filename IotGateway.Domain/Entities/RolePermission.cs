// ============================================================
// 文件：IotGateway.Domain/Entities/RolePermission.cs
// 用途：角色-权限关联实体（多对多中间表）
//       映射到数据库 role_permissions 表
// 所属层：Domain（领域层）
// ============================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IotGateway.Domain.Entities;

/// <summary>
/// 角色-权限关联实体（映射到 role_permissions 表）
/// 多对多关系：一个角色可以有多个权限，一个权限可以分配给多个角色
/// </summary>
[Table("role_permissions")]
public class RolePermission
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>角色 ID（外键 → roles.id）</summary>
    [Column("role_id")]
    public long RoleId { get; set; }

    /// <summary>权限 ID（外键 → permissions.id）</summary>
    [Column("permission_id")]
    public long PermissionId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
