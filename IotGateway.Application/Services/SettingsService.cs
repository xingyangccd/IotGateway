using IotGateway.Application.DTOs;
using IotGateway.Common.Exceptions;
using IotGateway.Domain.Entities;
using IotGateway.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IotGateway.Application.Services;

public sealed class SettingsService : ISettingsService
{
    private readonly AppDbContext _db;

    public SettingsService(AppDbContext db) => _db = db;

    public async Task<BasicSettingsDto> GetBasicAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = await _db.Set<BasicSettings>().AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
            return ToDto(settings ?? new BasicSettings());
        }
        catch
        {
            // Never pass provider messages to the existing exception filter.
            throw new BusinessException("基本设置暂时不可用", 503);
        }
    }

    public async Task<BasicSettingsDto> UpdateBasicAsync(
        UpdateBasicSettingsDto settings, CancellationToken cancellationToken = default)
    {
        if (settings is null || string.IsNullOrWhiteSpace(settings.SystemName)
            || settings.SystemName.Length > 100 || settings.SystemName.Any(char.IsControl)
            || settings.DataRetentionDays is < 7 or > 365
            || settings.AlarmCooldown is < 10 or > 3600)
            throw new BusinessException("基本设置无效：系统名称为 1–100 个字符，保留天数为 7–365，告警冷却时间为 10–3600 秒");

        try
        {
            var entity = await _db.Set<BasicSettings>()
                .SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
            if (entity is null)
            {
                entity = new BasicSettings();
                _db.Set<BasicSettings>().Add(entity);
            }

            entity.SystemName = settings.SystemName.Trim();
            entity.DataRetentionDays = settings.DataRetentionDays;
            entity.AlarmCooldown = settings.AlarmCooldown;
            await _db.SaveChangesAsync(cancellationToken);
            return ToDto(entity);
        }
        catch
        {
            throw new BusinessException("基本设置保存暂时不可用", 503);
        }
    }

    private static BasicSettingsDto ToDto(BasicSettings settings) =>
        new(settings.SystemName, settings.DataRetentionDays, settings.AlarmCooldown);
}
