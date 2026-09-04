using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qec.Itmg.Operations.Domain;

namespace Qec.Itmg.Operations.Persistence.Configurations;

internal sealed class OperationalEventConfiguration : IEntityTypeConfiguration<OperationalEvent>
{
    public void Configure(EntityTypeBuilder<OperationalEvent> builder)
    {
        builder.ToTable("OperationalEvent");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.EventNumber).IsRequired().HasMaxLength(32).HasColumnType("nvarchar(32)");
        builder.Property(item => item.Source).IsRequired().HasMaxLength(64).HasColumnType("nvarchar(64)");
        builder.Property(item => item.SourceEventKey).IsRequired().HasMaxLength(256).HasColumnType("nvarchar(256)");
        builder.Property(item => item.Severity).IsRequired().HasConversion<string>().HasMaxLength(32).HasColumnType("nvarchar(32)");
        builder.Property(item => item.Title).IsRequired().HasMaxLength(256).HasColumnType("nvarchar(256)");
        builder.Property(item => item.Summary).IsRequired().HasMaxLength(2000).HasColumnType("nvarchar(2000)");
        builder.Property(item => item.Status).IsRequired().HasConversion<string>().HasMaxLength(32).HasColumnType("nvarchar(32)");
        builder.Property(item => item.OccurrenceCount).IsRequired();
        builder.Property(item => item.FirstSeenAtUtc).IsRequired();
        builder.Property(item => item.LastSeenAtUtc).IsRequired();
        builder.Property(item => item.CreatedAtUtc).IsRequired();
        builder.Property(item => item.UpdatedAtUtc).IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();

        builder.HasIndex(item => item.EventNumber).IsUnique().HasDatabaseName("IX_OperationalEvent_EventNumber");
        builder.HasIndex(item => new { item.Source, item.SourceEventKey })
            .IsUnique()
            .HasDatabaseName("IX_OperationalEvent_Source_SourceEventKey");
        builder.HasIndex(item => new { item.Status, item.LastSeenAtUtc })
            .HasDatabaseName("IX_OperationalEvent_Status_LastSeenAtUtc");
        builder.HasIndex(item => new { item.Status, item.UpdatedAtUtc })
            .HasDatabaseName("IX_OperationalEvent_Status_UpdatedAtUtc");
        builder.HasIndex(item => item.ConfigurationItemId).HasDatabaseName("IX_OperationalEvent_ConfigurationItemId");
    }
}
