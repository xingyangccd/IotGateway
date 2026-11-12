// ============================================================
// 文件：IotGateway.Infrastructure/Repositories/UnitOfWork.cs
// 用途：工作单元实现，封装 AppDbContext + 所有 Repository
// 所属层：Infrastructure（基础设施层）
//
// 实现要点：
//   1. 持有 AppDbContext 实例（由 DI 注入）
//   2. 每个 Repository 属性使用懒加载（用到时才创建）
//   3. SaveChangesAsync 调用 _context.SaveChangesAsync()
//   4. 事务 API 直接委托给 EF Core 的 Database.BeginTransactionAsync()
// ============================================================

using Microsoft.EntityFrameworkCore.Storage;
using IotGateway.Domain.Entities;
using IotGateway.Infrastructure.Data;

namespace IotGateway.Infrastructure.Repositories;

/// <summary>
/// 工作单元实现
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private IDbContextTransaction? _transaction;    // EF Core 数据库事务对象

    // ===== 私有字段（懒加载模式）=====
    private IRepository<User>? _users;
    private IRepository<AuditLog>? _auditLogs;
    private IRepository<Device>? _devices;
    private IRepository<DeviceConfig>? _deviceConfigs;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    // ===== 仓储属性（懒加载：首次访问时创建，后续直接复用）=====

    public IRepository<User> Users =>
        _users ??= new Repository<User>(_context);

    public IRepository<AuditLog> AuditLogs =>
        _auditLogs ??= new Repository<AuditLog>(_context);

    public IRepository<Device> Devices =>
        _devices ??= new Repository<Device>(_context);

    public IRepository<DeviceConfig> DeviceConfigs =>
        _deviceConfigs ??= new Repository<DeviceConfig>(_context);

    // ===== 事务提交 =====

    /// <summary>
    /// 保存所有变更到数据库
    /// 在同一 DbContext 实例上执行，保证事务一致性
    /// </summary>
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
        // SaveChangesAsync：将所有追踪的实体变更（Added/Modified/Deleted）
        // 翻译为对应的 INSERT/UPDATE/DELETE SQL 语句并执行
        // 返回受影响的行数
    }

    // ===== 手动事务管理 =====

    /// <summary>
    /// 开启手动事务
    /// 使用场景：需要精确控制事务边界，或嵌套多次 SaveChanges
    /// </summary>
    public async Task BeginTransactionAsync()
    {
        _transaction = await _context.Database.BeginTransactionAsync();
        // BeginTransactionAsync：发送 BEGIN TRANSACTION 到 MySQL
    }

    /// <summary>
    /// 提交事务
    /// </summary>
    public async Task CommitTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync();
            // CommitAsync：发送 COMMIT 到 MySQL
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    /// <summary>
    /// 回滚事务
    /// 异常处理时调用，撤销所有未提交的变更
    /// </summary>
    public async Task RollbackTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync();
            // RollbackAsync：发送 ROLLBACK 到 MySQL
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    /// <summary>
    /// 释放资源（AppDbContext + 事务）
    /// </summary>
    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}
