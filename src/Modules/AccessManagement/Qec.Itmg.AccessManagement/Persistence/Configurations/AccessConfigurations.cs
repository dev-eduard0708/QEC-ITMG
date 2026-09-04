using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qec.Itmg.AccessManagement.Domain;

namespace Qec.Itmg.AccessManagement.Persistence.Configurations;

internal sealed class AccessCaseConfiguration : IEntityTypeConfiguration<AccessCase>
{
    public void Configure(EntityTypeBuilder<AccessCase> builder)
    {
        builder.ToTable("AccessCase");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CaseNumber).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Type).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.SubjectName).HasMaxLength(256);
        builder.Property(x => x.SubjectEmail).HasMaxLength(256);
        builder.Property(x => x.Reason).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(x => x.CaseNumber).IsUnique().HasDatabaseName("IX_AccessCase_CaseNumber");
        builder.HasIndex(x => new { x.Status, x.Type }).HasDatabaseName("IX_AccessCase_Status_Type");
        builder.HasIndex(x => x.RequesterUserId).HasDatabaseName("IX_AccessCase_RequesterUserId");
        builder.HasIndex(x => x.SubjectUserId).HasDatabaseName("IX_AccessCase_SubjectUserId");
        builder.HasIndex(x => x.EffectiveAtUtc).HasDatabaseName("IX_AccessCase_EffectiveAtUtc");
    }
}

internal sealed class AccessCaseItemConfiguration : IEntityTypeConfiguration<AccessCaseItem>
{
    public void Configure(EntityTypeBuilder<AccessCaseItem> builder)
    {
        builder.ToTable("AccessCaseItem");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EntitlementKey).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Action).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.HasIndex(x => new { x.AccessCaseId, x.Status }).HasDatabaseName("IX_AccessCaseItem_Case_Status");
        builder.HasIndex(x => x.EntitlementKey).HasDatabaseName("IX_AccessCaseItem_EntitlementKey");
        builder.HasOne<AccessCase>().WithMany().HasForeignKey(x => x.AccessCaseId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ExistingAccessSnapshotItemConfiguration : IEntityTypeConfiguration<ExistingAccessSnapshotItem>
{
    public void Configure(EntityTypeBuilder<ExistingAccessSnapshotItem> builder)
    {
        builder.ToTable("ExistingAccessSnapshotItem");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EntitlementKey).IsRequired().HasMaxLength(256);
        builder.Property(x => x.AccessSummary).HasMaxLength(1000);
        builder.HasIndex(x => x.AccessCaseId).HasDatabaseName("IX_ExistingAccessSnapshotItem_Case");
        builder.HasOne<AccessCase>().WithMany().HasForeignKey(x => x.AccessCaseId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AccessCaseExceptionConfiguration : IEntityTypeConfiguration<AccessCaseException>
{
    public void Configure(EntityTypeBuilder<AccessCaseException> builder)
    {
        builder.ToTable("AccessCaseException");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Reason).IsRequired().HasMaxLength(2000);
        builder.HasIndex(x => new { x.AccessCaseId, x.Type }).HasDatabaseName("IX_AccessCaseException_Case_Type");
        builder.HasOne<AccessCase>().WithMany().HasForeignKey(x => x.AccessCaseId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AccessReviewCampaignConfiguration : IEntityTypeConfiguration<AccessReviewCampaign>
{
    public void Configure(EntityTypeBuilder<AccessReviewCampaign> builder)
    {
        builder.ToTable("AccessReviewCampaign");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Type).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(x => new { x.Status, x.DueAtUtc }).HasDatabaseName("IX_AccessReviewCampaign_Status_Due");
        builder.HasIndex(x => x.ReviewerUserId).HasDatabaseName("IX_AccessReviewCampaign_Reviewer");
    }
}

internal sealed class AccessReviewItemConfiguration : IEntityTypeConfiguration<AccessReviewItem>
{
    public void Configure(EntityTypeBuilder<AccessReviewItem> builder)
    {
        builder.ToTable("AccessReviewItem");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AccessSummary).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.Decision).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.ReviewerComment).HasMaxLength(2000);
        builder.HasIndex(x => new { x.CampaignId, x.Decision }).HasDatabaseName("IX_AccessReviewItem_Campaign_Decision");
        builder.HasOne<AccessReviewCampaign>().WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ManagedAccountConfiguration : IEntityTypeConfiguration<ManagedAccount>
{
    public void Configure(EntityTypeBuilder<ManagedAccount> builder)
    {
        builder.ToTable("ManagedAccount");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AccountName).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Type).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Purpose).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(x => new { x.Type, x.Status }).HasDatabaseName("IX_ManagedAccount_Type_Status");
        builder.HasIndex(x => x.AccountName).HasDatabaseName("IX_ManagedAccount_AccountName");
        builder.HasIndex(x => x.OwnerUserId).HasDatabaseName("IX_ManagedAccount_OwnerUserId");
    }
}

internal sealed class SodRuleConfiguration : IEntityTypeConfiguration<SodRule>
{
    public void Configure(EntityTypeBuilder<SodRule> builder)
    {
        builder.ToTable("SodRule");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(256);
        builder.Property(x => x.LeftEntitlementKey).IsRequired().HasMaxLength(256);
        builder.Property(x => x.RightEntitlementKey).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Severity).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.HasIndex(x => x.IsActive).HasDatabaseName("IX_SodRule_IsActive");
        builder.HasIndex(x => new { x.LeftEntitlementKey, x.RightEntitlementKey })
            .HasDatabaseName("IX_SodRule_Left_Right");
    }
}
