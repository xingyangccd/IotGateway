// ============================================================
// 文件：IotGateway.Domain/Entities/AuditLog.cs
// 用途：操作审计日志实体，映射到数据库 audit_logs 表
// 所属层：Domain（领域层）
// ============================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IotGateway.Domain.Entities;

/// <summary>
/// 审计日志实体（映射到 audit_logs 表）
/// 记录所有用户操作，用于安全审计和问题追溯
/// </summary>
[Table("audit_logs")]
public class AuditLog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>操作用户 ID（外键 → users.id）</summary>
    [Column("user_id")]
    public long UserId { get; set; }

    /// <summary>操作用户名（冗余字段，方便查询）</summary>
    [Required]
    [MaxLength(50)]
    [Column("username")]
    public string Username { get; set; } = string.Empty;

    /// <summary>操作类型：Login/Create/Update/Delete/Control</summary>
    [Required]
    [MaxLength(50)]
    [Column("action_type")]
    public string ActionType { get; set; } = string.Empty;

    /// <summary>操作目标类型：Device/Alarm/User/Report</summary>
    [Required]
    [MaxLength(50)]
    [Column("target_type")]
    public string TargetType { get; set; } = string.Empty;

    /// <summary>操作目标 ID</summary>
    [Required]
    [MaxLength(100)]
    [Column("target_id")]
    public string TargetId { get; set; } = string.Empty;

    /// <summary>操作描述</summary>
    [Required]
    [MaxLength(500)]
    [Column("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>请求来源 IP</summary>
    [Required]
    [MaxLength(50)]
    [Column("request_ip")]
    public string RequestIp { get; set; } = string.Empty;

    /// <summary>请求参数快照（JSON 格式）</summary>
    [Column("request_params")]
    public string? RequestParams { get; set; }

    /// <summary>操作结果：Success / Failed</summary>
    [Required]
    [MaxLength(20)]
    [Column("result")]
    public string Result { get; set; } = "Success";

    /// <summary>错误消息（操作失败时记录）</summary>
    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    /// <summary>记录创建时间</summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
