using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qec.Itmg.DocumentManagement.Domain;

namespace Qec.Itmg.DocumentManagement.Persistence.Configurations;

internal sealed class ManagedDocumentConfiguration : IEntityTypeConfiguration<ManagedDocument>
{
    public void Configure(EntityTypeBuilder<ManagedDocument> builder)
    {
        builder.ToTable("ManagedDocument");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DocumentNumber).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(512);
        builder.Property(x => x.DocumentType).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Classification).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.RetirementReason).HasMaxLength(2000);
        builder.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(x => x.DocumentNumber).IsUnique().HasDatabaseName("IX_ManagedDocument_DocumentNumber");
        builder.HasIndex(x => x.DocumentType).HasDatabaseName("IX_ManagedDocument_DocumentType");
        builder.HasIndex(x => x.Status).HasDatabaseName("IX_ManagedDocument_Status");
        builder.HasIndex(x => x.OwnerUserId).HasDatabaseName("IX_ManagedDocument_OwnerUserId");
        builder.HasIndex(x => x.ReviewDate).HasDatabaseName("IX_ManagedDocument_ReviewDate");
        builder.HasIndex(x => x.Classification).HasDatabaseName("IX_ManagedDocument_Classification");
    }
}

internal sealed class DocumentVersionConfiguration : IEntityTypeConfiguration<DocumentVersion>
{
    public void Configure(EntityTypeBuilder<DocumentVersion> builder)
    {
        builder.ToTable("DocumentVersion");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ChangeSummary).HasMaxLength(2000);
        builder.HasIndex(x => new { x.ManagedDocumentId, x.VersionNumber })
            .IsUnique()
            .HasDatabaseName("IX_DocumentVersion_Document_Version");
        builder.HasOne<ManagedDocument>().WithMany().HasForeignKey(x => x.ManagedDocumentId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PolicyAcknowledgementConfiguration : IEntityTypeConfiguration<PolicyAcknowledgement>
{
    public void Configure(EntityTypeBuilder<PolicyAcknowledgement> builder)
    {
        builder.ToTable("PolicyAcknowledgement");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.DocumentVersionId, x.UserId })
            .IsUnique()
            .HasDatabaseName("IX_PolicyAcknowledgement_Version_User");
        builder.HasIndex(x => new { x.ManagedDocumentId, x.UserId })
            .HasDatabaseName("IX_PolicyAcknowledgement_Document_User");
        builder.HasOne<ManagedDocument>().WithMany().HasForeignKey(x => x.ManagedDocumentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<DocumentVersion>().WithMany().HasForeignKey(x => x.DocumentVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DocumentReviewNotificationLogConfiguration : IEntityTypeConfiguration<DocumentReviewNotificationLog>
{
    public void Configure(EntityTypeBuilder<DocumentReviewNotificationLog> builder)
    {
        builder.ToTable("DocumentReviewNotificationLog");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.ManagedDocumentId, x.ReviewDateUtc, x.ThresholdDays })
            .IsUnique()
            .HasDatabaseName("IX_DocumentReviewNotificationLog_Doc_Date_Threshold");
        builder.HasOne<ManagedDocument>().WithMany().HasForeignKey(x => x.ManagedDocumentId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class DocumentGovernanceLinkConfiguration : IEntityTypeConfiguration<DocumentGovernanceLink>
{
    public void Configure(EntityTypeBuilder<DocumentGovernanceLink> builder)
    {
        builder.ToTable("DocumentGovernanceLink");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.LinkKind).IsRequired().HasMaxLength(64);
        builder.Property(x => x.TargetKey).IsRequired().HasMaxLength(128);
        builder.HasIndex(x => new { x.ManagedDocumentId, x.LinkKind, x.TargetKey })
            .HasDatabaseName("IX_DocumentGovernanceLink_Doc_Kind_Target");
        builder.HasOne<ManagedDocument>().WithMany().HasForeignKey(x => x.ManagedDocumentId).OnDelete(DeleteBehavior.Cascade);
    }
}
