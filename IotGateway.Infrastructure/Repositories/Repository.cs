// ============================================================
// 文件：IotGateway.Infrastructure/Repositories/Repository.cs
// 用途：通用仓储实现，封装 EF Core DbContext 的 CRUD 操作
// 所属层：Infrastructure（基础设施层）
//
// 实现说明：
//   Repository<TEntity> 持有 AppDbContext 实例
//   所有方法都通过 _dbSet 操作数据库
//   这是一个具体的实现类，供 UnitOfWork 使用
// ============================================================

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using IotGateway.Infrastructure.Data;

namespace IotGateway.Infrastructure.Repositories;

/// <summary>
/// 通用仓储基类
/// 封装所有实体的通用 CRUD 逻辑
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
public class Repository<TEntity> : IRepository<TEntity> where TEntity : class
{
    protected readonly AppDbContext _context;       // 数据库上下文
    protected readonly DbSet<TEntity> _dbSet;       // 当前表对应的 DbSet

    /// <summary>
    /// 构造函数
    /// AppDbContext 由 DI 容器注入（生命周期：Scoped，每个请求一个实例）
    /// </summary>
    public Repository(AppDbContext context)
    {
        _context = context;
        _dbSet = context.Set<TEntity>();            // 获取 TEntity 对应的 DbSet
    }

    // ===== 查询实现 =====

    public async Task<TEntity?> GetByIdAsync(long id)
    {
        return await _dbSet.FindAsync(id);
        // FindAsync：先查 EF Core 内存缓存（变更追踪器），缓存未命中再查数据库
        // 比 FirstOrDefaultAsync 效率高（有缓存时）
    }

    public async Task<List<TEntity>> GetAllAsync()
    {
        return await _dbSet.ToListAsync();
        // ToListAsync：将 IQueryable 转为 List（实际执行 SQL）
    }

    public async Task<List<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate)
    {
        return await _dbSet.Where(predicate).ToListAsync();
    }

    public async Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate)
    {
        return await _dbSet.FirstOrDefaultAsync(predicate);
        // FirstOrDefault：返回第一条匹配记录，没有则返回 null
        // First：没有匹配时抛异常（不推荐）
    }

    public async Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate)
    {
        return await _dbSet.AnyAsync(predicate);
        // AnyAsync：使用 EXISTS 子查询，比 CountAsync > 0 效率高
    }

    public async Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null)
    {
        return predicate == null
            ? await _dbSet.CountAsync()
            : await _dbSet.CountAsync(predicate);
    }

    // ===== 分页查询 =====

    public async Task<(List<TEntity> Items, int TotalCount)> GetPagedAsync(
        int pageIndex, int pageSize,
        Expression<Func<TEntity, bool>>? predicate = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null)
    {
        var query = _dbSet.AsQueryable();           // 构建 IQueryable 查询

        if (predicate != null)
            query = query.Where(predicate);         // 添加 WHERE 条件

        var totalCount = await query.CountAsync();  // 先统计总数

        if (orderBy != null)
            query = orderBy(query);                 // 添加 ORDER BY
        // 注意：orderBy 传的是委托，不是表达式
        // 例如：q => q.OrderBy(u => u.Username)

        var items = await query
            .Skip((pageIndex - 1) * pageSize)       // 跳过前 N 页
            .Take(pageSize)                         // 取当前页
            .ToListAsync();

        return (items, totalCount);
    }

    // ===== 写操作 =====

    public async Task AddAsync(TEntity entity)
    {
        await _dbSet.AddAsync(entity);
        // AddAsync：将实体标记为 Added 状态
        // 注意：此时还没有执行 SQL！需要调用 SaveChangesAsync()
    }

    public async Task AddRangeAsync(IEnumerable<TEntity> entities)
    {
        await _dbSet.AddRangeAsync(entities);
        // AddRange：批量添加，生成一条 INSERT 语句（合并多个 VALUES）
    }

    public void Update(TEntity entity)
    {
        _dbSet.Update(entity);
        // Update：将实体标记为 Modified 状态
        // SaveChanges 时生成 UPDATE 语句
    }

    public void Delete(TEntity entity)
    {
        _dbSet.Remove(entity);
        // Remove：将实体标记为 Deleted 状态
        // SaveChanges 时生成 DELETE 语句
    }

    public void DeleteRange(IEnumerable<TEntity> entities)
    {
        _dbSet.RemoveRange(entities);
    }
}
