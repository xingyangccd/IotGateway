using IotGateway.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IotGateway.Infrastructure.Data;

public static class CoreOperationsModelConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<KnowledgeDocument>(entity =>
        {
            entity.HasIndex(x => x.ParentId);
            entity.HasIndex(x => new { x.Kind, x.UpdatedAt });
            entity.HasOne<KnowledgeDocument>().WithMany().HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.Version).IsConcurrencyToken();
        });
        modelBuilder.Entity<ReportArtifact>(entity =>
        {
            entity.HasIndex(x => new { x.CreatorId, x.ReportType, x.GeneratedAt, x.Id });
        });
    }
}
