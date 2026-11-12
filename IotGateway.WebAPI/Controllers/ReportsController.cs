using System.Globalization;
using System.Security.Claims;
using System.Text;
using IotGateway.Common.Exceptions;
using IotGateway.Common.Helpers;
using IotGateway.Domain.Entities;
using IotGateway.Infrastructure.Data;
using IotGateway.WebAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IotGateway.WebAPI.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Policy = "ReportsRead")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ReportsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ReportsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? type = "daily",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var reportType = ValidateType(type);
        if (page < 1 || pageSize < 1 || pageSize > 100 ||
            (long)(page - 1) * pageSize > int.MaxValue)
            throw new ValidationException("page 必须为正整数，pageSize 必须在 1–100 之间，且偏移量不能过大。");

        var creatorId = CreatorId();
        var query = _db.Set<ReportArtifact>().AsNoTracking()
            .Where(x => x.CreatorId == creatorId && x.ReportType == reportType);
        var total = await query.CountAsync(cancellationToken);
        var items = await Metadata(query.OrderByDescending(x => x.GeneratedAt).ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize).Take(pageSize))
            .ToListAsync(cancellationToken);
        AddDownloadUrls(items);
        return Ok(ApiResponse.Ok(new { items, total, page, pageSize }));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken cancellationToken)
    {
        var item = await Metadata(Owned(id)).SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("报表不存在。");
        AddDownloadUrls(new[] { item });
        return Ok(ApiResponse.Ok(item));
    }

    [HttpPost("generate")]
    [Authorize(Policy = "ReportsWrite")]
    [RequestSizeLimit(4096)]
    public async Task<IActionResult> Generate(
        [FromBody] GenerateCsvReportRequest? request, CancellationToken cancellationToken)
    {
        var creatorId = CreatorId();
        if (request is null)
            throw new ValidationException("请求不能为空。");
        var reportType = ValidateType(request.ReportType);
        if (request.Formats is not { Length: 1 } ||
            !string.Equals(request.Formats[0], "csv", StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("仅支持 formats: [\"csv\"]；不支持 Excel、XLSX、PDF 或空格式。");
        if (request.DateRange is not { Length: 2 } ||
            !TryDate(request.DateRange[0], out var start) ||
            !TryDate(request.DateRange[1], out var end) ||
            end < start || (end - start).TotalDays >= 366 || end.Date == DateTime.MaxValue.Date)
            throw new ValidationException("dateRange 必须为两个 YYYY-MM-DD 日期（含首尾），开始不晚于结束，最多 366 天。");

        // ReportsRead/ReportsWrite grant this allowlisted reporting dataset, not arbitrary entity exports.
        var content = await ReportCsvGenerator.GenerateAsync(
            _db, reportType, start, end.AddDays(1), cancellationToken);
        var now = DateTime.UtcNow;
        var artifact = new ReportArtifact
        {
            CreatorId = creatorId,
            ReportType = reportType,
            RangeStart = start,
            RangeEnd = end,
            GeneratedAt = now,
            FileName = FormattableString.Invariant(
                $"{reportType}_{start:yyyyMMdd}_{end:yyyyMMdd}_{now:yyyyMMddHHmmssfff}.csv"),
            Content = content,
            FileSize = content.LongLength,
            ContentType = "text/csv; charset=utf-8"
        };
        _db.Set<ReportArtifact>().Add(artifact);
        await _db.SaveChangesAsync(cancellationToken);
        var item = new CsvReportItem
        {
            Id = artifact.Id,
            FileName = artifact.FileName,
            ReportType = artifact.ReportType,
            GeneratedAt = artifact.GeneratedAt,
            FileSize = artifact.FileSize,
            DownloadUrl = ArtifactUrl(artifact.Id, "download")
        };
        return Ok(ApiResponse.Ok(item, "CSV 报表已生成。"));
    }

    [HttpGet("{id:long}/download")]
    public async Task<IActionResult> Download(long id, CancellationToken cancellationToken)
    {
        var artifact = await Owned(id).SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("报表不存在。");
        SetContentSecurityHeaders();
        return File(artifact.Content, artifact.ContentType, artifact.FileName);
    }

    [HttpGet("{id:long}/preview")]
    public async Task<IActionResult> Preview(long id, CancellationToken cancellationToken)
    {
        var content = await Owned(id).Select(x => x.Content).SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("报表不存在。");
        SetContentSecurityHeaders();
        return Content(Encoding.UTF8.GetString(content).TrimStart('\uFEFF'), "text/plain; charset=utf-8");
    }

    [HttpGet("{id:long}/preview-url")]
    public async Task<IActionResult> PreviewUrl(long id, CancellationToken cancellationToken)
    {
        await RequireOwned(id, cancellationToken);
        return Ok(ApiResponse.Ok(new { url = ArtifactUrl(id, "preview") }));
    }

    [HttpGet("{id:long}/download-url")]
    public async Task<IActionResult> DownloadUrl(long id, CancellationToken cancellationToken)
    {
        await RequireOwned(id, cancellationToken);
        return Ok(ApiResponse.Ok(new { url = ArtifactUrl(id, "download") }));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = "ReportsWrite")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await RequireOwned(id, cancellationToken);
        _db.Set<ReportArtifact>().Remove(new ReportArtifact { Id = id, CreatorId = CreatorId() });
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse.Ok(message: "报表已删除。"));
    }

    private long CreatorId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0)
            throw new UnauthorizedException("缺少有效的用户 ID。");
        return id;
    }

    private IQueryable<ReportArtifact> Owned(long id)
    {
        var creatorId = CreatorId();
        return _db.Set<ReportArtifact>().AsNoTracking().Where(x => x.Id == id && x.CreatorId == creatorId);
    }

    private async Task RequireOwned(long id, CancellationToken cancellationToken)
    {
        if (!await Owned(id).AnyAsync(cancellationToken))
            throw new NotFoundException("报表不存在。");
    }

    private static IQueryable<CsvReportItem> Metadata(IQueryable<ReportArtifact> query) =>
        query.Select(x => new CsvReportItem
        {
            Id = x.Id,
            FileName = x.FileName,
            ReportType = x.ReportType,
            GeneratedAt = x.GeneratedAt,
            FileSize = x.FileSize
        });

    private void AddDownloadUrls(IEnumerable<CsvReportItem> items)
    {
        foreach (var item in items)
            item.DownloadUrl = ArtifactUrl(item.Id, "download");
    }

    private string ArtifactUrl(long id, string action) =>
        $"{Request.PathBase}/api/reports/{id.ToString(CultureInfo.InvariantCulture)}/{action}";

    private void SetContentSecurityHeaders()
    {
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Security-Policy"] = "sandbox; default-src 'none'; frame-ancestors 'self'";
        Response.Headers["Cache-Control"] = "no-store";
    }

    private static string ValidateType(string? type)
    {
        var normalized = type?.Trim().ToLowerInvariant();
        return normalized is "daily" or "weekly" or "monthly" or "oee" or "alarm"
            ? normalized
            : throw new ValidationException("reportType/type 必须为 daily、weekly、monthly、oee 或 alarm。");
    }

    private static bool TryDate(string? value, out DateTime date) =>
        DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
}

public sealed class GenerateCsvReportRequest
{
    public string? ReportType { get; set; }
    public string?[]? DateRange { get; set; }
    public string?[]? Formats { get; set; }
}

public sealed class CsvReportItem
{
    public long Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ReportType { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public long FileSize { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
}
