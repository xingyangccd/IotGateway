// ============================================================
// 文件：IotGateway.Domain/Entities/UserRole.cs
// 用途：用户-角色关联实体（多对多中间表）
//       映射到数据库 user_roles 表
// 所属层：Domain（领域层）
// ============================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IotGateway.Domain.Entities;

/// <summary>
/// 用户-角色关联实体（映射到 user_roles 表）
/// 多对多关系：一个用户可以有多个角色，一个角色可以分配给多个用户
/// </summary>
[Table("user_roles")]
public class UserRole
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>用户 ID（外键 → users.id）</summary>
    [Column("user_id")]
    public long UserId { get; set; }

    /// <summary>角色 ID（外键 → roles.id）</summary>
    [Column("role_id")]
    public long RoleId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
