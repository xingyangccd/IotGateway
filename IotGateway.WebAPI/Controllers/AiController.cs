using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using IotGateway.Common.Exceptions;
using IotGateway.Common.Helpers;
using IotGateway.WebAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IotGateway.WebAPI.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize(Policy = "AiRead")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[RequestSizeLimit(32 * 1024)]
public sealed class AiController(OpenAiCompatibleClient provider, KnowledgeBaseStore knowledge) : ControllerBase
{
    [HttpPost("query")]
    public async Task<IActionResult> Query(AiQueryRequest request, CancellationToken ct)
    {
        ValidateText(request.Question, "question", 4000);
        return await AnswerAsync(request.Question!, request.Question!, ct);
    }

    [HttpPost("diagnose")]
    public async Task<IActionResult> Diagnose(AiDiagnosisRequest request, CancellationToken ct)
    {
        ValidateText(request.DeviceType, "deviceType", 100);
        ValidateText(request.DeviceCode, "deviceCode", 50);
        ValidateText(request.FaultDescription, "faultDescription", 4000);
        if (string.IsNullOrWhiteSpace(request.OccurredAt) ||
            !DateTimeOffset.TryParse(request.OccurredAt, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces, out var occurredAt) || occurredAt > DateTimeOffset.UtcNow.AddMinutes(5))
            throw new ValidationException("occurredAt 必须为有效的故障发生时间，且不能晚于当前时间。");
        var question = JsonSerializer.Serialize(new
        {
            request.DeviceType, request.DeviceCode, request.FaultDescription,
            occurredAt, note = "设备及故障信息由用户提供，未经实时设备数据核实。"
        });
        return await AnswerAsync(question, $"{request.DeviceType} {request.FaultDescription}", ct);
    }

    private async Task<IActionResult> AnswerAsync(string question, string search, CancellationToken ct)
    {
        provider.EnsureConfigured();
        var sources = await knowledge.RetrieveAsync(search, 5, ct);
        if (sources.Count == 0)
            throw new BusinessException("知识库中未检索到相关资料；请补充文档或调整问题。未生成无依据回答。", 422);
        var evidence = JsonSerializer.Serialize(sources.Select(x => new
        {
            x.Reference, x.Excerpt, x.UpdatedAt
        }));
        var answer = await provider.CompleteAsync(question, evidence, ct);
        var ids = sources.Select(x => x.Id).ToHashSet();
        foreach (Match citation in Regex.Matches(answer, @"\[KB:(\d+)\]"))
        {
            if (!long.TryParse(citation.Groups[1].Value, out var id) || !ids.Contains(id))
                throw new BusinessException("AI 提供方返回了不在检索结果中的引用。", 502);
        }
        return Ok(ApiResponse.Ok(new { answer, references = sources.Select(x => x.Reference).ToArray() }));
    }

    private static void ValidateText(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength)
            throw new ValidationException($"{field} 必须为 1–{maxLength} 个字符。");
    }
}

public sealed class AiQueryRequest
{
    public string? Question { get; set; }
}

public sealed class AiDiagnosisRequest
{
    public string? DeviceType { get; set; }
    public string? DeviceCode { get; set; }
    public string? FaultDescription { get; set; }
    public string? OccurredAt { get; set; }
}
