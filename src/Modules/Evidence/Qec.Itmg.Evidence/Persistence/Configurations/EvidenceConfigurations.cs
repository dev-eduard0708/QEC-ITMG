using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qec.Itmg.Evidence.Domain;

namespace Qec.Itmg.Evidence.Persistence.Configurations;

internal sealed class EvidenceRecordConfiguration : IEntityTypeConfiguration<EvidenceRecord>
{
    public void Configure(EntityTypeBuilder<EvidenceRecord> builder)
    {
        builder.ToTable("Evidence");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EvidenceNumber).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(512);
        builder.Property(x => x.Description).HasMaxLength(4000);
        builder.Property(x => x.SourceType).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.EvidenceType).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Classification).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.WithdrawalReason).HasMaxLength(2000);
        builder.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(x => x.EvidenceNumber).IsUnique().HasDatabaseName("IX_Evidence_EvidenceNumber");
        builder.HasIndex(x => x.Status).HasDatabaseName("IX_Evidence_Status");
        builder.HasIndex(x => x.OwnerUserId).HasDatabaseName("IX_Evidence_OwnerUserId");
        builder.HasIndex(x => x.Classification).HasDatabaseName("IX_Evidence_Classification");
        builder.HasIndex(x => x.ValidTo).HasDatabaseName("IX_Evidence_ValidTo");
        builder.HasIndex(x => new { x.SourceType, x.SourceRecordId }).HasDatabaseName("IX_Evidence_Source");
    }
}

internal sealed class EvidenceVersionConfiguration : IEntityTypeConfiguration<EvidenceVersion>
{
    public void Configure(EntityTypeBuilder<EvidenceVersion> builder)
    {
        builder.ToTable("EvidenceVersion");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ChangeSummary).HasMaxLength(2000);
        builder.HasIndex(x => new { x.EvidenceId, x.VersionNumber })
            .IsUnique()
            .HasDatabaseName("IX_EvidenceVersion_Evidence_Version");
        builder.HasOne<EvidenceRecord>().WithMany().HasForeignKey(x => x.EvidenceId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class EvidenceLinkConfiguration : IEntityTypeConfiguration<EvidenceLink>
{
    public void Configure(EntityTypeBuilder<EvidenceLink> builder)
    {
        builder.ToTable("EvidenceLink");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TargetType).IsRequired().HasConversion<string>().HasMaxLength(64);
        builder.HasIndex(x => new { x.EvidenceId, x.TargetType, x.TargetId })
            .IsUnique()
            .HasDatabaseName("IX_EvidenceLink_Evidence_Target");
        builder.HasIndex(x => new { x.TargetType, x.TargetId }).HasDatabaseName("IX_EvidenceLink_Target");
        builder.HasOne<EvidenceRecord>().WithMany().HasForeignKey(x => x.EvidenceId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class EvidenceExpiryNotificationLogConfiguration : IEntityTypeConfiguration<EvidenceExpiryNotificationLog>
{
    public void Configure(EntityTypeBuilder<EvidenceExpiryNotificationLog> builder)
    {
        builder.ToTable("EvidenceExpiryNotificationLog");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.EvidenceId, x.ValidToUtc, x.ThresholdDays })
            .IsUnique()
            .HasDatabaseName("IX_EvidenceExpiryNotificationLog_Evidence_Date_Threshold");
        builder.HasOne<EvidenceRecord>().WithMany().HasForeignKey(x => x.EvidenceId).OnDelete(DeleteBehavior.Cascade);
    }
}
