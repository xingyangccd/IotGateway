using IotGateway.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IotGateway.Infrastructure.Data;

public static class CoreAdministrationModelConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BasicSettings>(entity =>
        {
            entity.ToTable("basic_settings", table =>
            {
                table.HasCheckConstraint("ck_basic_settings_singleton", "id = 1");
                table.HasCheckConstraint("ck_basic_settings_retention", "data_retention_days BETWEEN 7 AND 365");
                table.HasCheckConstraint("ck_basic_settings_cooldown", "alarm_cooldown BETWEEN 10 AND 3600");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(x => x.SystemName).HasColumnName("system_name").HasMaxLength(100).IsRequired();
            entity.Property(x => x.DataRetentionDays).HasColumnName("data_retention_days").IsRequired();
            entity.Property(x => x.AlarmCooldown).HasColumnName("alarm_cooldown").IsRequired();
            entity.HasData(new BasicSettings());
        });
    }
}
