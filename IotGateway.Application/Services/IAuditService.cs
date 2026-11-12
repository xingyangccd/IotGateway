using IotGateway.Application.DTOs;

namespace IotGateway.Application.Services;

public interface IAuditService
{
    Task<AuditPageDto> GetLogsAsync(
        AuditLogQueryDto query, CancellationToken cancellationToken = default);
}
