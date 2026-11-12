using IotGateway.Application.DTOs;

namespace IotGateway.Application.Services;

public interface ISettingsService
{
    Task<BasicSettingsDto> GetBasicAsync(CancellationToken cancellationToken = default);
    Task<BasicSettingsDto> UpdateBasicAsync(
        UpdateBasicSettingsDto settings, CancellationToken cancellationToken = default);
}
