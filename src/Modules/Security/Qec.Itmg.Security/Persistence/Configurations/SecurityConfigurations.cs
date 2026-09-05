using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qec.Itmg.Security.Domain;

namespace Qec.Itmg.Security.Persistence.Configurations;

internal sealed class VulnerabilityConfiguration : IEntityTypeConfiguration<Vulnerability>
{
    public void Configure(EntityTypeBuilder<Vulnerability> builder)
    {
        builder.ToTable("Vulnerability");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.VulnerabilityNumber).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(512);
        builder.Property(x => x.Description).HasMaxLength(8000);
        builder.Property(x => x.Source).IsRequired().HasMaxLength(128);
        builder.Property(x => x.ExternalReference).HasMaxLength(512);
        builder.Property(x => x.Severity).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.ResolutionSummary).HasMaxLength(4000);
        builder.Property(x => x.AcceptedRiskReason).HasMaxLength(4000);
        builder.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(x => x.VulnerabilityNumber).IsUnique().HasDatabaseName("IX_Vulnerability_Number");
        builder.HasIndex(x => x.ConfigurationItemId).HasDatabaseName("IX_Vulnerability_CI");
        builder.HasIndex(x => x.Status).HasDatabaseName("IX_Vulnerability_Status");
        builder.HasIndex(x => x.Severity).HasDatabaseName("IX_Vulnerability_Severity");
        builder.HasIndex(x => x.DueAtUtc).HasDatabaseName("IX_Vulnerability_DueAtUtc");
    }
}

internal sealed class VulnerabilityRemediationLinkConfiguration : IEntityTypeConfiguration<VulnerabilityRemediationLink>
{
    public void Configure(EntityTypeBuilder<VulnerabilityRemediationLink> builder)
    {
        builder.ToTable("VulnerabilityRemediationLink");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.LinkType).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.HasIndex(x => new { x.VulnerabilityId, x.LinkType, x.TargetId })
            .IsUnique()
            .HasDatabaseName("IX_VulnerabilityRemediationLink_Unique");
        builder.HasOne<Vulnerability>().WithMany().HasForeignKey(x => x.VulnerabilityId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class RiskConfiguration : IEntityTypeConfiguration<Risk>
{
    public void Configure(EntityTypeBuilder<Risk> builder)
    {
        builder.ToTable("Risk");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RiskNumber).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(512);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(8000);
        builder.Property(x => x.Category).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Treatment).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.TreatmentPlan).HasMaxLength(4000);
        builder.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(x => x.RiskNumber).IsUnique().HasDatabaseName("IX_Risk_Number");
        builder.HasIndex(x => x.Status).HasDatabaseName("IX_Risk_Status");
        builder.HasIndex(x => x.OwnerUserId).HasDatabaseName("IX_Risk_Owner");
        builder.HasIndex(x => x.InherentScore).HasDatabaseName("IX_Risk_InherentScore");
        builder.HasIndex(x => x.ResidualScore).HasDatabaseName("IX_Risk_ResidualScore");
    }
}

internal sealed class RiskLinkConfiguration : IEntityTypeConfiguration<RiskLink>
{
    public void Configure(EntityTypeBuilder<RiskLink> builder)
    {
        builder.ToTable("RiskLink");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TargetType).IsRequired().HasMaxLength(64);
        builder.HasIndex(x => new { x.RiskId, x.TargetType, x.TargetId })
            .IsUnique()
            .HasDatabaseName("IX_RiskLink_Unique");
        builder.HasOne<Risk>().WithMany().HasForeignKey(x => x.RiskId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PolicyExceptionConfiguration : IEntityTypeConfiguration<PolicyException>
{
    public void Configure(EntityTypeBuilder<PolicyException> builder)
    {
        builder.ToTable("PolicyException");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExceptionNumber).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(512);
        builder.Property(x => x.Reason).IsRequired().HasMaxLength(8000);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.CompensatingControls).HasMaxLength(4000);
        builder.Property(x => x.RejectionReason).HasMaxLength(2000);
        builder.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(x => x.ExceptionNumber).IsUnique().HasDatabaseName("IX_PolicyException_Number");
        builder.HasIndex(x => x.Status).HasDatabaseName("IX_PolicyException_Status");
        builder.HasIndex(x => x.ExpiresAtUtc).HasDatabaseName("IX_PolicyException_ExpiresAtUtc");
    }
}

internal sealed class PenetrationTestConfiguration : IEntityTypeConfiguration<PenetrationTest>
{
    public void Configure(EntityTypeBuilder<PenetrationTest> builder)
    {
        builder.ToTable("PenetrationTest");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PentestNumber).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(512);
        builder.Property(x => x.Provider).HasMaxLength(256);
        builder.Property(x => x.ScopeSummary).IsRequired().HasMaxLength(4000);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(x => x.PentestNumber).IsUnique().HasDatabaseName("IX_PenetrationTest_Number");
        builder.HasIndex(x => x.Status).HasDatabaseName("IX_PenetrationTest_Status");
        builder.HasIndex(x => x.StartDate).HasDatabaseName("IX_PenetrationTest_StartDate");
    }
}

internal sealed class PentestFindingConfiguration : IEntityTypeConfiguration<PentestFinding>
{
    public void Configure(EntityTypeBuilder<PentestFinding> builder)
    {
        builder.ToTable("PentestFinding");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(512);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(8000);
        builder.Property(x => x.Severity).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(x => new { x.PenetrationTestId, x.Status }).HasDatabaseName("IX_PentestFinding_Test_Status");
        builder.HasOne<PenetrationTest>().WithMany().HasForeignKey(x => x.PenetrationTestId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AwarenessCampaignConfiguration : IEntityTypeConfiguration<AwarenessCampaign>
{
    public void Configure(EntityTypeBuilder<AwarenessCampaign> builder)
    {
        builder.ToTable("AwarenessCampaign");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(512);
        builder.Property(x => x.Description).HasMaxLength(4000);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(x => x.Status).HasDatabaseName("IX_AwarenessCampaign_Status");
        builder.HasIndex(x => x.DueAtUtc).HasDatabaseName("IX_AwarenessCampaign_DueAtUtc");
        builder.HasIndex(x => x.ModuleId).HasDatabaseName("IX_AwarenessCampaign_ModuleId");
    }
}

internal sealed class AwarenessCompletionConfiguration : IEntityTypeConfiguration<AwarenessCompletion>
{
    public void Configure(EntityTypeBuilder<AwarenessCompletion> builder)
    {
        builder.ToTable("AwarenessCompletion");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.HasIndex(x => new { x.CampaignId, x.UserId })
            .IsUnique()
            .HasDatabaseName("IX_AwarenessCompletion_Campaign_User");
        builder.HasIndex(x => x.UserId).HasDatabaseName("IX_AwarenessCompletion_UserId");
        builder.HasIndex(x => x.DueAtUtc).HasDatabaseName("IX_AwarenessCompletion_DueAtUtc");
        builder.HasIndex(x => x.Status).HasDatabaseName("IX_AwarenessCompletion_Status");
        builder.HasOne<AwarenessCampaign>().WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AwarenessModuleConfiguration : IEntityTypeConfiguration<AwarenessModule>
{
    public void Configure(EntityTypeBuilder<AwarenessModule> builder)
    {
        builder.ToTable("AwarenessModule");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Summary).HasMaxLength(1000);
        builder.Property(x => x.Body).IsRequired();
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("IX_AwarenessModule_Code");
        builder.HasIndex(x => x.Status).HasDatabaseName("IX_AwarenessModule_Status");
    }
}

internal sealed class AwarenessQuestionConfiguration : IEntityTypeConfiguration<AwarenessQuestion>
{
    public void Configure(EntityTypeBuilder<AwarenessQuestion> builder)
    {
        builder.ToTable("AwarenessQuestion");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.QuestionText).IsRequired().HasMaxLength(1000);
        builder.HasIndex(x => new { x.ModuleId, x.DisplayOrder }).HasDatabaseName("IX_AwarenessQuestion_Module_Order");
        builder.HasOne<AwarenessModule>().WithMany().HasForeignKey(x => x.ModuleId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AwarenessAnswerOptionConfiguration : IEntityTypeConfiguration<AwarenessAnswerOption>
{
    public void Configure(EntityTypeBuilder<AwarenessAnswerOption> builder)
    {
        builder.ToTable("AwarenessAnswerOption");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Text).IsRequired().HasMaxLength(500);
        builder.HasIndex(x => new { x.QuestionId, x.DisplayOrder }).HasDatabaseName("IX_AwarenessAnswer_Question_Order");
        builder.HasOne<AwarenessQuestion>().WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AwarenessAttemptConfiguration : IEntityTypeConfiguration<AwarenessAttempt>
{
    public void Configure(EntityTypeBuilder<AwarenessAttempt> builder)
    {
        builder.ToTable("AwarenessAttempt");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.AssignmentId).HasDatabaseName("IX_AwarenessAttempt_AssignmentId");
        builder.HasIndex(x => new { x.AssignmentId, x.AttemptNumber })
            .IsUnique()
            .HasDatabaseName("IX_AwarenessAttempt_Assignment_Number");
        builder.HasOne<AwarenessCompletion>().WithMany().HasForeignKey(x => x.AssignmentId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AwarenessReminderLogConfiguration : IEntityTypeConfiguration<AwarenessReminderLog>
{
    public void Configure(EntityTypeBuilder<AwarenessReminderLog> builder)
    {
        builder.ToTable("AwarenessReminderLog");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ReminderKind).IsRequired().HasMaxLength(32);
        builder.HasIndex(x => new { x.AssignmentId, x.ReminderKind })
            .IsUnique()
            .HasDatabaseName("IX_AwarenessReminder_Assignment_Kind");
        builder.HasOne<AwarenessCompletion>().WithMany().HasForeignKey(x => x.AssignmentId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ExceptionExpiryNotificationLogConfiguration : IEntityTypeConfiguration<ExceptionExpiryNotificationLog>
{
    public void Configure(EntityTypeBuilder<ExceptionExpiryNotificationLog> builder)
    {
        builder.ToTable("ExceptionExpiryNotificationLog");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventKey).IsRequired().HasMaxLength(64);
        builder.HasIndex(x => new { x.ExceptionId, x.EventKey })
            .IsUnique()
            .HasDatabaseName("IX_ExceptionExpiryNotificationLog_Unique");
        builder.HasOne<PolicyException>().WithMany().HasForeignKey(x => x.ExceptionId).OnDelete(DeleteBehavior.Cascade);
    }
}
