// ============================================================
// 文件：IotGateway.Domain/Entities/User.cs
// 用途：用户实体类，映射到数据库 users 表
// 所属层：Domain（领域层）—— 纯 POCO，不依赖任何框架
// ============================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IotGateway.Domain.Entities;

/// <summary>
/// 用户实体（映射到 users 表）
///
/// Data Annotations（数据注解）说明：
///   [Table("表名")]          → 指定映射的数据库表名
///   [Key]                    → 标记为主键（PRIMARY KEY）
///   [Column("列名")]         → 指定映射的数据库列名
///                             C# 属性用 PascalCase（如 PasswordHash）
///                             数据库列用 snake_case（如 password_hash）
///   [Required]               → NOT NULL 约束
///   [MaxLength(50)]          → VARCHAR(50)
///   [DatabaseGenerated]      → 数据库自动生成值（自增 IDENTITY）
/// </summary>
[Table("users")]
public class User
{
    /// <summary>用户唯一ID（自增主键）</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>登录用户名（唯一，用于登录）</summary>
    [Required]
    [MaxLength(50)]
    [Column("username")]
    public string Username { get; set; } = string.Empty;

    /// <summary>密码哈希值（BCrypt 加密存储，不存明文）</summary>
    [Required]
    [MaxLength(255)]
    [Column("password_hash")]
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>真实姓名（显示名称）</summary>
    [Required]
    [MaxLength(50)]
    [Column("real_name")]
    public string RealName { get; set; } = string.Empty;

    /// <summary>邮箱地址</summary>
    [MaxLength(100)]
    [Column("email")]
    public string Email { get; set; } = string.Empty;

    /// <summary>手机号码</summary>
    [MaxLength(20)]
    [Column("phone")]
    public string Phone { get; set; } = string.Empty;

    /// <summary>头像图片 URL</summary>
    [MaxLength(500)]
    [Column("avatar_url")]
    public string AvatarUrl { get; set; } = string.Empty;

    /// <summary>
    /// 角色：Admin = 管理员, Engineer = 工程师, Operator = 操作员
    /// 当前阶段用字符串字段，RBAC 完整实现后改为关联 roles 表
    /// </summary>
    [Required]
    [MaxLength(20)]
    [Column("role")]
    public string Role { get; set; } = "Operator";

    /// <summary>账户状态：1 = 启用, 0 = 禁用</summary>
    [Column("status")]
    public sbyte Status { get; set; } = 1;

    /// <summary>最后登录时间（登录成功后更新）</summary>
    [Column("last_login_at")]
    public DateTime? LastLoginAt { get; set; }

    /// <summary>记录创建时间</summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>记录更新时间（由数据库 ON UPDATE CURRENT_TIMESTAMP 自动更新）</summary>
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
