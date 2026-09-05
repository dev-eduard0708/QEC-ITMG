using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qec.Itmg.BusinessContinuity.Domain;

namespace Qec.Itmg.BusinessContinuity.Persistence.Configurations;

internal sealed class BiaRecordConfiguration : IEntityTypeConfiguration<BiaRecord>
{
    public void Configure(EntityTypeBuilder<BiaRecord> builder)
    {
        builder.ToTable("BiaRecord");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.BiaNumber).IsRequired().HasMaxLength(32);
        builder.Property(x => x.BusinessProcessName).HasMaxLength(512);
        builder.Property(x => x.BusinessImpactSummary).IsRequired().HasMaxLength(8000);
        builder.Property(x => x.FinancialImpact).HasMaxLength(2000);
        builder.Property(x => x.OperationalImpact).HasMaxLength(2000);
        builder.Property(x => x.RegulatoryImpact).HasMaxLength(2000);
        builder.Property(x => x.ReputationalImpact).HasMaxLength(2000);
        builder.Property(x => x.Criticality).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(x => x.BiaNumber).IsUnique().HasDatabaseName("IX_BiaRecord_Number");
        builder.HasIndex(x => new { x.BusinessServiceId, x.Status }).HasDatabaseName("IX_BiaRecord_Service_Status");
    }
}

internal sealed class ContinuityPlanConfiguration : IEntityTypeConfiguration<ContinuityPlan>
{
    public void Configure(EntityTypeBuilder<ContinuityPlan> builder)
    {
        builder.ToTable("ContinuityPlan");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PlanNumber).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(512);
        builder.Property(x => x.PlanType).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(x => x.PlanNumber).IsUnique().HasDatabaseName("IX_ContinuityPlan_Number");
        builder.HasIndex(x => new { x.PlanType, x.Status }).HasDatabaseName("IX_ContinuityPlan_Type_Status");
        builder.HasIndex(x => x.ReviewAtUtc).HasDatabaseName("IX_ContinuityPlan_ReviewAtUtc");
    }
}

internal sealed class ContinuityScopeLinkConfiguration : IEntityTypeConfiguration<ContinuityScopeLink>
{
    public void Configure(EntityTypeBuilder<ContinuityScopeLink> builder)
    {
        builder.ToTable("ContinuityScopeLink");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OwnerType).IsRequired().HasMaxLength(64);
        builder.Property(x => x.TargetType).IsRequired().HasConversion<string>().HasMaxLength(64);
        builder.HasIndex(x => new { x.OwnerId, x.OwnerType, x.TargetType, x.TargetId })
            .IsUnique()
            .HasDatabaseName("IX_ContinuityScopeLink_Unique");
    }
}

internal sealed class RecoveryProcedureConfiguration : IEntityTypeConfiguration<RecoveryProcedure>
{
    public void Configure(EntityTypeBuilder<RecoveryProcedure> builder)
    {
        builder.ToTable("RecoveryProcedure");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProcedureNumber).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(512);
        builder.Property(x => x.RecoveryStage).HasMaxLength(128);
        builder.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(x => x.ProcedureNumber).IsUnique().HasDatabaseName("IX_RecoveryProcedure_Number");
        builder.HasIndex(x => new { x.ContinuityPlanId, x.Sequence }).HasDatabaseName("IX_RecoveryProcedure_Plan_Sequence");
        builder.HasOne<ContinuityPlan>().WithMany().HasForeignKey(x => x.ContinuityPlanId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class DrTestConfiguration : IEntityTypeConfiguration<DrTest>
{
    public void Configure(EntityTypeBuilder<DrTest> builder)
    {
        builder.ToTable("DrTest");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DrTestNumber).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(512);
        builder.Property(x => x.TestType).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Result).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Summary).HasMaxLength(8000);
        builder.Property(x => x.Gaps).HasMaxLength(8000);
        builder.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(x => x.DrTestNumber).IsUnique().HasDatabaseName("IX_DrTest_Number");
        builder.HasIndex(x => new { x.BusinessServiceId, x.Status }).HasDatabaseName("IX_DrTest_Service_Status");
        builder.HasIndex(x => x.PlannedAtUtc).HasDatabaseName("IX_DrTest_PlannedAtUtc");
    }
}

internal sealed class ContinuityNotificationLogConfiguration : IEntityTypeConfiguration<ContinuityNotificationLog>
{
    public void Configure(EntityTypeBuilder<ContinuityNotificationLog> builder)
    {
        builder.ToTable("ContinuityNotificationLog");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventKey).IsRequired().HasMaxLength(64);
        builder.HasIndex(x => new { x.ResourceId, x.EventKey })
            .IsUnique()
            .HasDatabaseName("IX_ContinuityNotificationLog_Unique");
    }
}
