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
        builder.Property(x => x.AccessCategoryKeySnapshot).HasMaxLength(64);
        builder.Property(x => x.AccessCategoryNameSnapshot).HasMaxLength(256);
        builder.Property(x => x.VerificationMethod).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.VerificationOutcome).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.VerificationComment).HasMaxLength(2000);
        builder.Property(x => x.FallbackReason).HasMaxLength(2000);
        builder.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.Ignore(x => x.IsReadyToClose);
        builder.HasIndex(x => x.CaseNumber).IsUnique().HasDatabaseName("IX_AccessCase_CaseNumber");
        builder.HasIndex(x => new { x.Status, x.Type }).HasDatabaseName("IX_AccessCase_Status_Type");
        builder.HasIndex(x => x.RequesterUserId).HasDatabaseName("IX_AccessCase_RequesterUserId");
        builder.HasIndex(x => x.SubjectUserId).HasDatabaseName("IX_AccessCase_SubjectUserId");
        builder.HasIndex(x => x.EffectiveAtUtc).HasDatabaseName("IX_AccessCase_EffectiveAtUtc");
        builder.HasIndex(x => x.VendorId).HasDatabaseName("IX_AccessCase_VendorId");
        builder.HasIndex(x => x.AccessCategoryId).HasDatabaseName("IX_AccessCase_AccessCategoryId");
    }
}

internal sealed class AccessCategoryConfiguration : IEntityTypeConfiguration<AccessCategory>
{
    public void Configure(EntityTypeBuilder<AccessCategory> builder)
    {
        builder.ToTable("AccessCategory");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).IsRequired().HasMaxLength(64);
        builder.Property(x => x.NameEn).IsRequired().HasMaxLength(256);
        builder.Property(x => x.NameAr).IsRequired().HasMaxLength(256);
        builder.Property(x => x.DescriptionEn).HasMaxLength(2000);
        builder.Property(x => x.DescriptionAr).HasMaxLength(2000);
        builder.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(x => x.Key).IsUnique().HasDatabaseName("IX_AccessCategory_Key");
        builder.HasIndex(x => x.IsActive).HasDatabaseName("IX_AccessCategory_IsActive");
    }
}

internal sealed class AccessCategoryParticipantConfiguration : IEntityTypeConfiguration<AccessCategoryParticipant>
{
    public void Configure(EntityTypeBuilder<AccessCategoryParticipant> builder)
    {
        builder.ToTable("AccessCategoryParticipant");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Stage).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(x => new { x.AccessCategoryId, x.Stage, x.UserId })
            .IsUnique()
            .HasDatabaseName("IX_AccessCategoryParticipant_Category_Stage_User");
        builder.HasOne<AccessCategory>().WithMany().HasForeignKey(x => x.AccessCategoryId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AccessCaseRouteParticipantConfiguration : IEntityTypeConfiguration<AccessCaseRouteParticipant>
{
    public void Configure(EntityTypeBuilder<AccessCaseRouteParticipant> builder)
    {
        builder.ToTable("AccessCaseRouteParticipant");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Stage).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(x => new { x.AccessCaseId, x.Stage, x.UserId })
            .IsUnique()
            .HasDatabaseName("IX_AccessCaseRouteParticipant_Case_Stage_User");
        builder.HasIndex(x => new { x.UserId, x.Stage }).HasDatabaseName("IX_AccessCaseRouteParticipant_User_Stage");
        builder.HasOne<AccessCase>().WithMany().HasForeignKey(x => x.AccessCaseId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AccessCaseItemConfiguration : IEntityTypeConfiguration<AccessCaseItem>
{
    public void Configure(EntityTypeBuilder<AccessCaseItem> builder)
    {
        builder.ToTable("AccessCaseItem");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EntitlementKey).IsRequired().HasMaxLength(256);
        builder.Property(x => x.EntitlementNameEnSnapshot).HasMaxLength(256);
        builder.Property(x => x.EntitlementNameArSnapshot).HasMaxLength(256);
        builder.Property(x => x.Action).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.HasIndex(x => new { x.AccessCaseId, x.Status }).HasDatabaseName("IX_AccessCaseItem_Case_Status");
        builder.HasIndex(x => x.EntitlementKey).HasDatabaseName("IX_AccessCaseItem_EntitlementKey");
        builder.HasIndex(x => x.AccessEntitlementId).HasDatabaseName("IX_AccessCaseItem_AccessEntitlementId");
        builder.HasOne<AccessCase>().WithMany().HasForeignKey(x => x.AccessCaseId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<AccessEntitlement>().WithMany().HasForeignKey(x => x.AccessEntitlementId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class AccessEntitlementConfiguration : IEntityTypeConfiguration<AccessEntitlement>
{
    public void Configure(EntityTypeBuilder<AccessEntitlement> builder)
    {
        builder.ToTable("AccessEntitlement");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).IsRequired().HasMaxLength(64);
        builder.Property(x => x.NameEn).IsRequired().HasMaxLength(256);
        builder.Property(x => x.NameAr).IsRequired().HasMaxLength(256);
        builder.Property(x => x.DescriptionEn).HasMaxLength(2000);
        builder.Property(x => x.DescriptionAr).HasMaxLength(2000);
        builder.Property(x => x.DefaultRevokeAction).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(x => x.Key).IsUnique().HasDatabaseName("IX_AccessEntitlement_Key");
        builder.HasIndex(x => x.IsActive).HasDatabaseName("IX_AccessEntitlement_IsActive");
    }
}

internal sealed class AccessCategoryEntitlementConfiguration : IEntityTypeConfiguration<AccessCategoryEntitlement>
{
    public void Configure(EntityTypeBuilder<AccessCategoryEntitlement> builder)
    {
        builder.ToTable("AccessCategoryEntitlement");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.AccessCategoryId, x.AccessEntitlementId })
            .IsUnique()
            .HasDatabaseName("IX_AccessCategoryEntitlement_Category_Entitlement");
        builder.HasIndex(x => new { x.AccessCategoryId, x.SortOrder })
            .HasDatabaseName("IX_AccessCategoryEntitlement_Category_Sort");
        builder.HasOne<AccessCategory>().WithMany().HasForeignKey(x => x.AccessCategoryId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<AccessEntitlement>().WithMany().HasForeignKey(x => x.AccessEntitlementId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class UserAccessEntitlementConfiguration : IEntityTypeConfiguration<UserAccessEntitlement>
{
    public void Configure(EntityTypeBuilder<UserAccessEntitlement> builder)
    {
        builder.ToTable("UserAccessEntitlement");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EntitlementKeySnapshot).IsRequired().HasMaxLength(256);
        builder.Property(x => x.EntitlementNameSnapshot).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(x => new { x.UserId, x.Status }).HasDatabaseName("IX_UserAccessEntitlement_User_Status");
        builder.HasIndex(x => new { x.UserId, x.AccessEntitlementId })
            .IsUnique()
            .HasFilter("[AccessEntitlementId] IS NOT NULL AND [Status] = N'Active'")
            .HasDatabaseName("IX_UserAccessEntitlement_User_Entitlement_Active");
        builder.HasIndex(x => new { x.UserId, x.EntitlementKeySnapshot })
            .IsUnique()
            .HasFilter("[AccessEntitlementId] IS NULL AND [Status] = N'Active'")
            .HasDatabaseName("IX_UserAccessEntitlement_User_Key_Active");
        builder.HasOne<AccessEntitlement>().WithMany().HasForeignKey(x => x.AccessEntitlementId)
            .OnDelete(DeleteBehavior.SetNull);
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
        builder.HasIndex(x => x.VendorId).HasDatabaseName("IX_ManagedAccount_VendorId");
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
