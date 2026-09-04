using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qec.Itmg.Audit.Domain;

namespace Qec.Itmg.Audit.Persistence.Configurations;

internal sealed class AuditEngagementConfiguration : IEntityTypeConfiguration<AuditEngagement>
{
    public void Configure(EntityTypeBuilder<AuditEngagement> builder)
    {
        builder.ToTable("AuditEngagement");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AuditNumber).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(512);
        builder.Property(x => x.AuditType).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Objective).HasMaxLength(4000);
        builder.Property(x => x.ScopeSummary).HasMaxLength(4000);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(x => x.AuditNumber).IsUnique().HasDatabaseName("IX_AuditEngagement_AuditNumber");
        builder.HasIndex(x => x.Status).HasDatabaseName("IX_AuditEngagement_Status");
        builder.HasIndex(x => x.StartDate).HasDatabaseName("IX_AuditEngagement_StartDate");
        builder.HasIndex(x => x.EndDate).HasDatabaseName("IX_AuditEngagement_EndDate");
    }
}

internal sealed class AuditScopeLinkConfiguration : IEntityTypeConfiguration<AuditScopeLink>
{
    public void Configure(EntityTypeBuilder<AuditScopeLink> builder)
    {
        builder.ToTable("AuditScopeLink");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TargetType).IsRequired().HasConversion<string>().HasMaxLength(64);
        builder.HasIndex(x => new { x.AuditEngagementId, x.TargetType, x.TargetId })
            .IsUnique()
            .HasDatabaseName("IX_AuditScopeLink_Engagement_Target");
        builder.HasOne<AuditEngagement>().WithMany().HasForeignKey(x => x.AuditEngagementId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AuditQuestionConfiguration : IEntityTypeConfiguration<AuditQuestion>
{
    public void Configure(EntityTypeBuilder<AuditQuestion> builder)
    {
        builder.ToTable("AuditQuestion");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.QuestionCode).HasMaxLength(64);
        builder.Property(x => x.Category).IsRequired().HasMaxLength(128);
        builder.Property(x => x.QuestionText).IsRequired().HasMaxLength(4000);
        builder.Property(x => x.ResponseType).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Response).HasMaxLength(8000);
        builder.Property(x => x.ReviewerNotes).HasMaxLength(4000);
        builder.HasIndex(x => new { x.AuditEngagementId, x.SortOrder }).HasDatabaseName("IX_AuditQuestion_Engagement_Sort");
        builder.HasOne<AuditEngagement>().WithMany().HasForeignKey(x => x.AuditEngagementId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class FindingConfiguration : IEntityTypeConfiguration<Finding>
{
    public void Configure(EntityTypeBuilder<Finding> builder)
    {
        builder.ToTable("Finding");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FindingNumber).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(512);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(8000);
        builder.Property(x => x.Severity).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.AcceptedRiskReason).HasMaxLength(4000);
        builder.Property(x => x.ExceptionReference).HasMaxLength(512);
        builder.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(x => x.FindingNumber).IsUnique().HasDatabaseName("IX_Finding_FindingNumber");
        builder.HasIndex(x => new { x.AuditEngagementId, x.Status }).HasDatabaseName("IX_Finding_Engagement_Status");
        builder.HasIndex(x => x.Severity).HasDatabaseName("IX_Finding_Severity");
        builder.HasOne<AuditEngagement>().WithMany().HasForeignKey(x => x.AuditEngagementId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ManagementResponseConfiguration : IEntityTypeConfiguration<ManagementResponse>
{
    public void Configure(EntityTypeBuilder<ManagementResponse> builder)
    {
        builder.ToTable("ManagementResponse");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ResponseText).IsRequired().HasMaxLength(8000);
        builder.HasIndex(x => x.FindingId).HasDatabaseName("IX_ManagementResponse_FindingId");
        builder.HasOne<Finding>().WithMany().HasForeignKey(x => x.FindingId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CorrectiveActionConfiguration : IEntityTypeConfiguration<CorrectiveAction>
{
    public void Configure(EntityTypeBuilder<CorrectiveAction> builder)
    {
        builder.ToTable("CorrectiveAction");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ActionNumber).HasMaxLength(32);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(512);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(8000);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.VerificationNotes).HasMaxLength(4000);
        builder.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(x => x.ActionNumber).IsUnique().HasDatabaseName("IX_CorrectiveAction_ActionNumber")
            .HasFilter("[ActionNumber] IS NOT NULL");
        builder.HasIndex(x => new { x.FindingId, x.Status }).HasDatabaseName("IX_CorrectiveAction_Finding_Status");
        builder.HasIndex(x => x.DueAtUtc).HasDatabaseName("IX_CorrectiveAction_DueAtUtc");
        builder.HasOne<Finding>().WithMany().HasForeignKey(x => x.FindingId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class EvidenceRequestConfiguration : IEntityTypeConfiguration<EvidenceRequest>
{
    public void Configure(EntityTypeBuilder<EvidenceRequest> builder)
    {
        builder.ToTable("EvidenceRequest");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(512);
        builder.Property(x => x.Description).HasMaxLength(4000);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Notes).HasMaxLength(4000);
        builder.HasIndex(x => new { x.AuditEngagementId, x.Status }).HasDatabaseName("IX_EvidenceRequest_Engagement_Status");
        builder.HasIndex(x => x.DueAtUtc).HasDatabaseName("IX_EvidenceRequest_DueAtUtc");
        builder.HasOne<AuditEngagement>().WithMany().HasForeignKey(x => x.AuditEngagementId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class EvidenceRequestNotificationLogConfiguration : IEntityTypeConfiguration<EvidenceRequestNotificationLog>
{
    public void Configure(EntityTypeBuilder<EvidenceRequestNotificationLog> builder)
    {
        builder.ToTable("EvidenceRequestNotificationLog");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventKey).IsRequired().HasMaxLength(64);
        builder.HasIndex(x => new { x.EvidenceRequestId, x.EventKey })
            .IsUnique()
            .HasDatabaseName("IX_EvidenceRequestNotificationLog_Request_Event");
        builder.HasOne<EvidenceRequest>().WithMany().HasForeignKey(x => x.EvidenceRequestId).OnDelete(DeleteBehavior.Cascade);
    }
}
