namespace IotGateway.Application.DTOs;

public sealed class AuditLogQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public long? UserId { get; set; }
    public string? ActionType { get; set; }
    public string? TargetType { get; set; }
    public long? TargetId { get; set; }
    public string? Result { get; set; }
    public DateTimeOffset? FromUtc { get; set; }
    public DateTimeOffset? ToUtc { get; set; }
}

// Username is a numeric subject label, never the legacy username or a name claim.
public sealed record AuditLogDto(
    long Id,
    long UserId,
    string Username,
    string ActionType,
    string TargetType,
    long? TargetId,
    string Result,
    int? HttpStatusCode,
    int? ApplicationCode,
    DateTime CreatedAt);

public sealed record AuditPageDto(
    IReadOnlyList<AuditLogDto> Items,
    long Total,
    int Page,
    int PageSize);
