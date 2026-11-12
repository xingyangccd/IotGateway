using System.Data;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using IotGateway.Common.Exceptions;
using IotGateway.Domain.Entities;
using IotGateway.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace IotGateway.WebAPI.Services;

// AiRead grants access to this shared knowledge corpus, not arbitrary operational or user tables.
public sealed class KnowledgeBaseStore(AppDbContext db)
{
    public IQueryable<KnowledgeDocument> Documents => db.Set<KnowledgeDocument>().AsNoTracking();

    public async Task<KnowledgeDocument> GetAsync(long id, CancellationToken ct) =>
        await Documents.SingleOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new NotFoundException("知识库文档不存在。");

    public async Task<IReadOnlyList<KnowledgeReference>> RetrieveAsync(string question, int limit, CancellationToken ct)
    {
        var terms = SearchTerms(question);
        if (terms.Length == 0)
            throw new ValidationException("查询必须包含文字或数字。");

        var document = Expression.Parameter(typeof(KnowledgeDocument), "document");
        Expression score = Expression.Constant(0);
        foreach (var term in terms)
        {
            foreach (var (field, weight) in new[] { (nameof(KnowledgeDocument.Name), 3), (nameof(KnowledgeDocument.Content), 1) })
            {
                var text = Expression.Call(Expression.Property(document, field), nameof(string.ToLower), Type.EmptyTypes);
                var contains = Expression.Call(text, nameof(string.Contains), Type.EmptyTypes, Expression.Constant(term));
                score = Expression.Add(score, Expression.Condition(contains, Expression.Constant(weight), Expression.Constant(0)));
            }
        }
        var matches = Expression.Lambda<Func<KnowledgeDocument, bool>>(
            Expression.GreaterThan(score, Expression.Constant(0)), document);
        var rank = Expression.Lambda<Func<KnowledgeDocument, int>>(score, document);
        var docs = await Documents.Where(x => x.Kind == "document").Where(matches)
            .OrderByDescending(rank).ThenByDescending(x => x.UpdatedAt).ThenBy(x => x.Id)
            .Take(limit).ToListAsync(ct);
        return docs.Select(x => new KnowledgeReference(x.Id, x.Name, Excerpt(x.Content, terms),
            $"[KB:{x.Id}] {x.Name}", x.UpdatedAt)).ToList();
    }

    public async Task<KnowledgeDocument> CreateAsync(KnowledgeWriteRequest request, long actorId, CancellationToken ct)
    {
        Validate(request);
        return await WriteAsync(async () =>
        {
            if (await Documents.CountAsync(ct) >= 2000)
                throw new BusinessException("知识库最多支持 2000 个节点。", 409);
            await ValidateParentAsync(request.ParentId, null, ct);
            var document = new KnowledgeDocument
            {
                ParentId = request.ParentId, Name = request.Name!.Trim(), Kind = request.Kind!,
                Content = request.Content?.Trim() ?? "", CreatedBy = actorId
            };
            db.Set<KnowledgeDocument>().Add(document);
            return document;
        }, ct);
    }

    public async Task<KnowledgeDocument> UpdateAsync(long id, KnowledgeWriteRequest request, CancellationToken ct)
    {
        Validate(request);
        RequireVersion(request.Version);
        return await WriteAsync(async () =>
        {
            var document = await db.Set<KnowledgeDocument>().SingleOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("知识库文档不存在。");
            if (document.Version != request.Version)
                throw new BusinessException("文档已被修改，请刷新后重试。", 409);
            if (document.Kind != request.Kind)
                throw new ValidationException("不能改变节点类型，请新建节点。");
            await ValidateParentAsync(request.ParentId, id, ct);
            document.ParentId = request.ParentId;
            document.Name = request.Name!.Trim();
            document.Content = request.Content?.Trim() ?? "";
            document.UpdatedAt = DateTime.UtcNow;
            document.Version = Guid.NewGuid().ToString("N");
            return document;
        }, ct);
    }

    public async Task DeleteAsync(long id, string? version, CancellationToken ct)
    {
        RequireVersion(version);
        await WriteAsync(async () =>
        {
            var document = await db.Set<KnowledgeDocument>().SingleOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("知识库文档不存在。");
            if (document.Version != version)
                throw new BusinessException("文档已被修改，请刷新后重试。", 409);
            if (await Documents.AnyAsync(x => x.ParentId == id, ct))
                throw new BusinessException("请先删除或移动子节点。", 409);
            db.Set<KnowledgeDocument>().Remove(document);
            return true;
        }, ct);
    }

    private async Task ValidateParentAsync(long? parentId, long? currentId, CancellationToken ct)
    {
        var visited = new HashSet<long>();
        while (parentId is { } id)
        {
            if (id <= 0 || id == currentId || !visited.Add(id) || visited.Count > 50)
                throw new ValidationException("知识库父节点无效、形成循环或超过 50 层。");
            var parent = await Documents.Where(x => x.Id == id)
                .Select(x => new { x.ParentId, x.Kind }).SingleOrDefaultAsync(ct)
                ?? throw new NotFoundException("父节点不存在。");
            if (parent.Kind != "folder")
                throw new ValidationException("父节点必须是文件夹。");
            parentId = parent.ParentId;
        }
    }

    private static void Validate(KnowledgeWriteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200 ||
            request.Name.Any(char.IsControl))
            throw new ValidationException("name 必须为 1–200 个字符且不能包含控制字符。");
        if (request.Kind is not ("document" or "folder"))
            throw new ValidationException("kind 必须为 document 或 folder。");
        if (request.Content?.Length > 20000 ||
            (request.Kind == "document" && string.IsNullOrWhiteSpace(request.Content)) ||
            (request.Kind == "folder" && !string.IsNullOrWhiteSpace(request.Content)))
            throw new ValidationException("文档 content 必须为 1–20000 个字符，文件夹 content 必须为空。");
    }

    private static void RequireVersion(string? version)
    {
        if (version is null || !Guid.TryParseExact(version, "N", out _))
            throw new ValidationException("请提供读取节点时返回的 version。");
    }

    private async Task<T> WriteAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var result = await action();
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessException("知识库数据已被修改，请刷新后重试。", 409);
        }
        catch (Exception exception) when (IsConflict(exception))
        {
            throw new BusinessException("知识库数据冲突，请刷新后重试。", 409);
        }
    }

    private static bool IsConflict(Exception exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
            if (current is MySqlException mysql && mysql.Number is 1062 or 1205 or 1213 or 1451 or 1452)
                return true;
        return false;
    }

    private static string[] SearchTerms(string question)
    {
        var terms = new List<string>();
        foreach (Match match in Regex.Matches(question.ToLowerInvariant(), @"[\p{L}\p{N}]+"))
        {
            var value = match.Value;
            if (value.Any(c => c is >= '\u3400' and <= '\u9fff'))
            {
                for (var i = 0; i < value.Length - 1 && terms.Count < 64; i++)
                    terms.Add(value.Substring(i, 2));
                if (value.Length == 1) terms.Add(value);
            }
            else terms.Add(value[..Math.Min(100, value.Length)]);
            if (terms.Count >= 64) break;
        }
        return terms.Distinct().Take(32).ToArray();
    }

    private static string Excerpt(string content, string[] terms)
    {
        if (content.Length <= 2000) return content;
        var index = terms.Select(term => content.IndexOf(term, StringComparison.OrdinalIgnoreCase))
            .Where(x => x >= 0).DefaultIfEmpty(0).Min();
        var start = Math.Max(0, index - 300);
        var length = Math.Min(2000, content.Length - start);
        return (start > 0 ? "…" : "") + content.Substring(start, length) +
            (start + length < content.Length ? "…" : "");
    }
}

public sealed class KnowledgeWriteRequest
{
    public long? ParentId { get; set; }
    public string? Name { get; set; }
    public string? Kind { get; set; } = "document";
    public string? Content { get; set; }
    public string? Version { get; set; }
}

public sealed record KnowledgeReference(long Id, string Name, string Excerpt, string Reference, DateTime UpdatedAt);
