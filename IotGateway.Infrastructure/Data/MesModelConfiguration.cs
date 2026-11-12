using IotGateway.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace IotGateway.Infrastructure.Data;

public static class MesModelConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MesProduct>(e =>
        {
            e.ToTable("mes_products");
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).IsRequired().HasMaxLength(64);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Specification).IsRequired().HasMaxLength(500);
            e.Property(x => x.Unit).IsRequired().HasMaxLength(32);
            e.Property(x => x.CycleTimeSeconds).HasPrecision(18, 3);
            e.Property(x => x.Version).IsConcurrencyToken();
        });
        modelBuilder.Entity<MesLine>(e =>
        {
            e.ToTable("mes_lines");
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).IsRequired().HasMaxLength(64);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Workshop).IsRequired().HasMaxLength(100);
            e.HasOne<Device>().WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Version).IsConcurrencyToken();
        });
        modelBuilder.Entity<MesOrder>(e =>
        {
            e.ToTable("mes_orders");
            e.HasKey(x => x.Id);
            e.Property(x => x.OrderNo).IsRequired().HasMaxLength(64);
            e.HasIndex(x => x.OrderNo).IsUnique();
            e.Property(x => x.BatchNo).IsRequired().HasMaxLength(100);
            e.HasIndex(x => x.BatchNo);
            e.Property(x => x.Status).IsRequired().HasMaxLength(24);
            e.HasIndex(x => new { x.Status, x.LineId });
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Line).WithMany().HasForeignKey(x => x.LineId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasQueryFilter(x => x.DeletedAt == null);
        });
        modelBuilder.Entity<MesProductionReport>(e =>
        {
            e.ToTable("mes_production_reports");
            e.HasKey(x => x.Id);
            e.HasOne<MesOrder>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.OrderNo).IsRequired().HasMaxLength(64);
            e.Property(x => x.RequestId).IsRequired().HasMaxLength(36);
            e.HasIndex(x => x.RequestId).IsUnique();
            e.Property(x => x.PayloadHash).IsRequired().HasMaxLength(64);
            e.Property(x => x.DefectReason).HasMaxLength(1000);
            e.Property(x => x.Station).HasMaxLength(100);
            e.Property(x => x.OperatorName).IsRequired().HasMaxLength(200);
            e.HasIndex(x => new { x.OrderId, x.ReportedAt });
            e.HasIndex(x => x.ReportedAt);
        });
        modelBuilder.Entity<MesInspection>(e =>
        {
            e.ToTable("mes_inspections");
            e.HasKey(x => x.Id);
            e.HasOne<MesOrder>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.OrderNo).IsRequired().HasMaxLength(64);
            e.Property(x => x.InspectionType).IsRequired().HasMaxLength(16);
            e.Property(x => x.Result).IsRequired().HasMaxLength(8);
            e.Property(x => x.Remark).HasMaxLength(2000);
            e.Property(x => x.InspectorName).IsRequired().HasMaxLength(200);
            e.HasIndex(x => new { x.OrderId, x.InspectionType, x.Id });
        });
        modelBuilder.Entity<MesOrderEvent>(e =>
        {
            e.ToTable("mes_order_events");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Action).IsRequired().HasMaxLength(32);
            e.Property(x => x.FromStatus).IsRequired().HasMaxLength(24);
            e.Property(x => x.ToStatus).IsRequired().HasMaxLength(24);
            e.Property(x => x.OperatorName).IsRequired().HasMaxLength(200);
            e.Property(x => x.Description).IsRequired().HasMaxLength(2000);
            e.HasIndex(x => new { x.OrderId, x.CreatedAt });
        });

        // 两种数据库读取的 DateTime 默认没有 Kind，统一恢复 UTC，避免响应丢失时区。
        var utc = new ValueConverter<DateTime, DateTime>(
            value => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value,
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
        foreach (var type in new[] { typeof(MesOrder), typeof(MesProductionReport), typeof(MesInspection), typeof(MesOrderEvent) })
        {
            foreach (var property in modelBuilder.Entity(type).Metadata.GetProperties())
            {
                if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                    property.SetValueConverter(utc);
            }
        }
    }
}
