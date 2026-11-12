// ============================================================
// 文件：IotGateway.Infrastructure/Data/AppDbContext.cs
// 用途：EF Core 数据库上下文
//       管理所有实体和数据库的连接、查询、事务
// 所属层：Infrastructure（基础设施层）
//
// DbContext 是 EF Core 的核心类，相当于一个"数据库会话"：
//   1. 管理数据库连接（通过连接字符串）
//   2. 暴露所有实体的 DbSet（每个 DbSet 代表一张表）
//   3. 将 LINQ 查询翻译为 SQL 语句
//   4. 追踪实体变更（哪些属性被修改了）
//   5. 管理事务（BeginTransaction / Commit / Rollback）
// ============================================================

using Microsoft.EntityFrameworkCore;
using IotGateway.Domain.Entities;

namespace IotGateway.Infrastructure.Data;

/// <summary>
/// 应用数据库上下文
/// 继承 DbContext → 获得所有 EF Core 数据库操作能力
/// </summary>
public class AppDbContext : DbContext
{
    /// <summary>
    /// 构造函数
    /// DbContextOptions<AppDbContext> 由 DI 容器传入
    /// 包含数据库连接字符串、Provider 类型等配置信息
    /// </summary>
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // ===== DbSet 属性（每个属性代表一张数据库表）=====

    public DbSet<User> Users => Set<User>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DeviceConfig> DeviceConfigs => Set<DeviceConfig>();
    public DbSet<DeviceData> DeviceData => Set<DeviceData>();
    public DbSet<ProductionRecord> ProductionRecords => Set<ProductionRecord>();
    public DbSet<EnergyRecord> EnergyRecords => Set<EnergyRecord>();
    public DbSet<AlarmRule> AlarmRules => Set<AlarmRule>();
    public DbSet<Alarm> Alarms => Set<Alarm>();
    public DbSet<MaintenancePlan> MaintenancePlans => Set<MaintenancePlan>();
    public DbSet<MaintenanceRecord> MaintenanceRecords => Set<MaintenanceRecord>();
    public DbSet<RepairOrder> RepairOrders => Set<RepairOrder>();
    public DbSet<SparePart> SpareParts => Set<SparePart>();
    public DbSet<SparePartRecord> SparePartRecords => Set<SparePartRecord>();
    public DbSet<FaultCase> FaultCases => Set<FaultCase>();
    public DbSet<AiAnalysisRecord> AiAnalysisRecords => Set<AiAnalysisRecord>();
    public DbSet<AiReport> AiReports => Set<AiReport>();
    public DbSet<DeviceHealthScore> DeviceHealthScores => Set<DeviceHealthScore>();

    // RBAC 新表（第四章新增）
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    // ===== Fluent API 模型配置 =====

    /// <summary>
    /// OnModelCreating：使用 Fluent API 配置实体和数据库表的映射关系
    ///
    /// Data Annotations vs Fluent API：
    ///   Data Annotations（[Table][Key][Column]）：简单直观，写在实体类上
    ///   Fluent API：功能更强大，所有配置集中在 OnModelCreating
    ///
    /// 本课程两者结合：
    ///   - 基本映射（表名、列名、主键）→ Data Annotations
    ///   - 复杂配置（索引、复合键、关系、默认值）→ Fluent API
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ===== User 实体配置 =====
        modelBuilder.Entity<User>(entity =>
        {
            // 唯一索引：用户名唯一
            entity.HasIndex(e => e.Username)
                .IsUnique()
                .HasDatabaseName("uk_username");

            // 普通索引：角色
            entity.HasIndex(e => e.Role)
                .HasDatabaseName("idx_role");

            // 普通索引：状态
            entity.HasIndex(e => e.Status)
                .HasDatabaseName("idx_status");
        });

        // ===== Device 实体配置 =====
        modelBuilder.Entity<Device>(entity =>
        {
            entity.HasIndex(e => e.DeviceCode)
                .IsUnique()
                .HasDatabaseName("uk_device_code");

            entity.HasIndex(e => new { e.Workshop, e.ProductionLine })
                .HasDatabaseName("idx_workshop_line");

            entity.HasIndex(e => e.Protocol)
                .HasDatabaseName("idx_protocol");
        });

        // ===== DeviceConfig：1:1 关联 Device =====
        modelBuilder.Entity<DeviceConfig>(entity =>
        {
            entity.HasIndex(e => e.DeviceId)
                .IsUnique()
                .HasDatabaseName("uk_device_id");
        });

        // ===== AuditLog 索引配置 =====
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(e => e.UserId).HasDatabaseName("idx_user_id");
            entity.HasIndex(e => e.ActionType).HasDatabaseName("idx_action_type");
            entity.HasIndex(e => e.CreatedAt).HasDatabaseName("idx_created_at");
            entity.HasIndex(e => new { e.TargetType, e.TargetId }).HasDatabaseName("idx_target");
        });

        // ===== RBAC 新表配置 =====
        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasIndex(e => e.Code)
                .IsUnique()
                .HasDatabaseName("uk_role_code");
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasIndex(e => e.Code)
                .IsUnique()
                .HasDatabaseName("uk_permission_code");
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.RoleId })
                .IsUnique()
                .HasDatabaseName("uk_user_role");
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasIndex(e => new { e.RoleId, e.PermissionId })
                .IsUnique()
                .HasDatabaseName("uk_role_permission");
        });
    }
}
