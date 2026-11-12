using System.Globalization;
using System.Security.Claims;
using IotGateway.Common.Exceptions;
using IotGateway.Common.Helpers;
using IotGateway.WebAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IotGateway.WebAPI.Controllers;

[ApiController]
[Route("api/knowledge-base")]
[Authorize(Policy = "AiRead")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class KnowledgeBaseController(KnowledgeBaseStore store) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            throw new ValidationException("分页参数无效；pageSize 必须在 1–100 之间。");
        var total = await store.Documents.CountAsync(ct);
        var items = await store.Documents.OrderBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, x.ParentId, x.Name, x.Kind, x.UpdatedAt, x.Version }).ToListAsync(ct);
        return Ok(ApiResponse.Ok(new { items, total, page, pageSize }));
    }

    [HttpGet("tree")]
    public async Task<IActionResult> Tree(CancellationToken ct)
    {
        var nodes = await store.Documents.OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Select(x => new KnowledgeTreeNode
            {
                Id = x.Id, ParentId = x.ParentId, Name = x.Name, Kind = x.Kind, Version = x.Version
            }).Take(2001).ToListAsync(ct);
        if (nodes.Count > 2000)
            throw new BusinessException("知识库节点过多，请使用分页列表。", 422);
        var byId = nodes.ToDictionary(x => x.Id);
        var roots = new List<KnowledgeTreeNode>();
        foreach (var node in nodes)
        {
            var visited = new HashSet<long> { node.Id };
            var parentId = node.ParentId;
            while (parentId is { } id && byId.TryGetValue(id, out var ancestor))
            {
                if (!visited.Add(id) || visited.Count > 51)
                    throw new BusinessException("知识库层级数据无效。", 409);
                parentId = ancestor.ParentId;
            }
            if (node.ParentId is { } parent && byId.TryGetValue(parent, out var folder))
                folder.Children.Add(node);
            else roots.Add(node);
        }
        return Ok(ApiResponse.Ok(roots));
    }

    [HttpGet("query")]
    public async Task<IActionResult> Query([FromQuery] string? question, [FromQuery] int limit = 5,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(question) || question.Length > 4000 || limit is < 1 or > 10)
            throw new ValidationException("question 必须为 1–4000 个字符，limit 必须在 1–10 之间。");
        return Ok(ApiResponse.Ok(await store.RetrieveAsync(question, limit, ct)));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await store.GetAsync(id, ct)));

    [HttpPost]
    [Authorize(Policy = "AiWrite")]
    [RequestSizeLimit(128 * 1024)]
    public async Task<IActionResult> Create(KnowledgeWriteRequest request, CancellationToken ct)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!long.TryParse(claim, NumberStyles.None, CultureInfo.InvariantCulture, out var actorId) || actorId <= 0)
            throw new UnauthorizedException("缺少有效用户 ID。");
        return Ok(ApiResponse.Ok(await store.CreateAsync(request, actorId, ct)));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = "AiWrite")]
    [RequestSizeLimit(128 * 1024)]
    public async Task<IActionResult> Update(long id, KnowledgeWriteRequest request, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await store.UpdateAsync(id, request, ct)));

    [HttpDelete("{id:long}")]
    [Authorize(Policy = "AiWrite")]
    public async Task<IActionResult> Delete(long id, [FromQuery] string? version, CancellationToken ct)
    {
        await store.DeleteAsync(id, version, ct);
        return Ok(ApiResponse.Ok(message: "知识库节点已删除。"));
    }
}

public sealed class KnowledgeTreeNode
{
    public long Id { get; set; }
    public long? ParentId { get; set; }
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Version { get; set; } = "";
    public List<KnowledgeTreeNode> Children { get; set; } = [];
}
