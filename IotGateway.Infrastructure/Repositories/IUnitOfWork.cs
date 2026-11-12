// ============================================================
// 文件：IotGateway.Infrastructure/Repositories/IUnitOfWork.cs
// 用途：工作单元接口，统一管理所有仓储和事务
// 所属层：Infrastructure（基础设施层）
//
// 工作单元解决的问题：
//   场景：用户登录需要同时：
//     ① 更新 users.last_login_at
//     ② 插入 audit_logs 登录记录
//
//   如果只用 Repository（没有 UnitOfWork）：
//     var userRepo = new UserRepository();
//     var logRepo = new AuditLogRepository();
//     userRepo.Update(user);   // ① 更新成功
//     logRepo.AddAsync(log);   // ② 插入失败（磁盘满了）
//     → users 表更新了但 audit_logs 表没记录 → 数据不一致！
//
//   用 UnitOfWork：
//     _unitOfWork.Users.Update(user);
//     _unitOfWork.AuditLogs.AddAsync(log);
//     await _unitOfWork.SaveChangesAsync();
//     → 两个操作在同一事务中，要么全成功，要么全回滚
// ============================================================

using IotGateway.Domain.Entities;

namespace IotGateway.Infrastructure.Repositories;

/// <summary>
/// 工作单元接口
/// </summary>
public interface IUnitOfWork : IDisposable
{
    // ===== 仓储属性（每个表对应一个 Repository）=====

    IRepository<User> Users { get; }
    IRepository<AuditLog> AuditLogs { get; }
    IRepository<Device> Devices { get; }
    IRepository<DeviceConfig> DeviceConfigs { get; }

    // ===== 事务提交 =====

    /// <summary>保存所有变更到数据库</summary>
    /// <returns>受影响的行数</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    // ===== 手动事务管理（用于跨多表操作）=====

    /// <summary>开启数据库事务</summary>
    Task BeginTransactionAsync();

    /// <summary>提交事务</summary>
    Task CommitTransactionAsync();

    /// <summary>回滚事务</summary>
    Task RollbackTransactionAsync();
}
