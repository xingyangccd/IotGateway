using IotGateway.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IotGateway.Infrastructure.Data;

public static class DeviceModelConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DeviceData>(entity =>
        {
            entity.Property(d => d.ParamValue).HasPrecision(18, 6);
            entity.HasIndex(d => new { d.DeviceId, d.ParamName, d.CollectedAt, d.Id });
            entity.HasOne<Device>().WithMany().HasForeignKey(d => d.DeviceId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<AlarmRule>(entity =>
        {
            entity.Property(r => r.ThresholdLower).HasPrecision(18, 6);
            entity.Property(r => r.ThresholdUpper).HasPrecision(18, 6);
            entity.HasIndex(r => new { r.DeviceType, r.ParamName, r.IsEnabled });
        });
        modelBuilder.Entity<Alarm>(entity =>
        {
            entity.Property(a => a.ParamValue).HasPrecision(18, 6);
            entity.Property(a => a.ThresholdValue).HasPrecision(18, 6);
            entity.HasIndex(a => new { a.DeviceId, a.RuleId, a.TriggeredAt });
            entity.HasIndex(a => new { a.Status, a.CreatedAt });
            entity.HasOne<Device>().WithMany().HasForeignKey(a => a.DeviceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AlarmRule>().WithMany().HasForeignKey(a => a.RuleId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
