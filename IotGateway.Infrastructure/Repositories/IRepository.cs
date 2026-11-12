// ============================================================
// 文件：IotGateway.Infrastructure/Repositories/IRepository.cs
// 用途：通用仓储接口，定义所有实体共用的 CRUD 操作
// 所属层：Infrastructure（基础设施层）
//
// 设计理念：
//   每个实体不需要单独写 {GetById, Add, Update, Delete} 方法
//   IRepository<T> 泛型接口提供所有实体的基本操作
//   特定查询通过 FindAsync(Expression) 方法实现
//
// 泛型约束 where T : class：
//   保证 T 是引用类型（Entity 都是 class）
//   没有这个约束，EF Core 的 DbSet<T> 无法工作
// ============================================================

using System.Linq.Expressions;

namespace IotGateway.Infrastructure.Repositories;

/// <summary>
/// 通用仓储接口
/// </summary>
/// <typeparam name="TEntity">实体类型（必须是 class）</typeparam>
public interface IRepository<TEntity> where TEntity : class
{
    // ===== 查询操作 =====

    /// <summary>根据主键 ID 查询单条记录</summary>
    Task<TEntity?> GetByIdAsync(long id);

    /// <summary>查询所有记录（慎用！数据量大时用分页）</summary>
    Task<List<TEntity>> GetAllAsync();

    /// <summary>根据条件查询（LINQ 表达式树）</summary>
    /// <example>
    /// var users = await _repo.FindAsync(u => u.Username == "admin");
    /// </example>
    Task<List<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate);

    /// <summary>根据条件查询第一条匹配记录</summary>
    Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate);

    /// <summary>判断是否存在满足条件的记录</summary>
    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate);

    /// <summary>统计满足条件的记录数</summary>
    Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null);

    // ===== 分页查询 =====

    /// <summary>分页查询（返回数据项 + 总记录数）</summary>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页记录数</param>
    /// <param name="predicate">筛选条件（可选）</param>
    /// <param name="orderBy">排序规则（可选）</param>
    /// <returns>(数据项列表, 总记录数) 元组</returns>
    Task<(List<TEntity> Items, int TotalCount)> GetPagedAsync(
        int pageIndex,
        int pageSize,
        Expression<Func<TEntity, bool>>? predicate = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null);

    // ===== 写操作 =====

    /// <summary>添加单条记录</summary>
    Task AddAsync(TEntity entity);

    /// <summary>批量添加记录</summary>
    Task AddRangeAsync(IEnumerable<TEntity> entities);

    /// <summary>更新记录（标记为 Modified 状态）</summary>
    void Update(TEntity entity);

    /// <summary>删除单条记录</summary>
    void Delete(TEntity entity);

    /// <summary>批量删除记录</summary>
    void DeleteRange(IEnumerable<TEntity> entities);
}
