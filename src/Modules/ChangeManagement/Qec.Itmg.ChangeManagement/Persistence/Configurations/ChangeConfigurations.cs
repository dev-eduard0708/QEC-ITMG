using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qec.Itmg.ChangeManagement.Domain;

namespace Qec.Itmg.ChangeManagement.Persistence.Configurations;

internal sealed class ChangeRequestConfiguration : IEntityTypeConfiguration<ChangeRequest>
{
    public void Configure(EntityTypeBuilder<ChangeRequest> builder)
    {
        builder.ToTable("ChangeRequest");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.ChangeNumber).IsRequired().HasMaxLength(32).HasColumnType("nvarchar(32)");
        builder.Property(item => item.Title).IsRequired().HasMaxLength(256).HasColumnType("nvarchar(256)");
        builder.Property(item => item.Description).IsRequired().HasMaxLength(4000).HasColumnType("nvarchar(4000)");
        builder.Property(item => item.Type).IsRequired().HasConversion<string>().HasMaxLength(32).HasColumnType("nvarchar(32)");
        builder.Property(item => item.Status).IsRequired().HasConversion<string>().HasMaxLength(32).HasColumnType("nvarchar(32)");
        builder.Property(item => item.RiskRating).IsRequired().HasConversion<string>().HasMaxLength(32).HasColumnType("nvarchar(32)");
        builder.Property(item => item.Result).IsRequired().HasConversion<string>().HasMaxLength(32).HasColumnType("nvarchar(32)");
        builder.Property(item => item.BusinessImpact).HasMaxLength(2000).HasColumnType("nvarchar(2000)");
        builder.Property(item => item.TechnicalImpact).HasMaxLength(2000).HasColumnType("nvarchar(2000)");
        builder.Property(item => item.SecurityImpact).HasMaxLength(2000).HasColumnType("nvarchar(2000)");
        builder.Property(item => item.ImplementationPlan).HasMaxLength(4000).HasColumnType("nvarchar(4000)");
        builder.Property(item => item.TestPlan).HasMaxLength(4000).HasColumnType("nvarchar(4000)");
        builder.Property(item => item.RollbackPlan).HasMaxLength(4000).HasColumnType("nvarchar(4000)");
        builder.Property(item => item.ValidationNotes).HasMaxLength(4000).HasColumnType("nvarchar(4000)");
        builder.Property(item => item.PirNotes).HasMaxLength(4000).HasColumnType("nvarchar(4000)");
        builder.Property(item => item.CreatedAtUtc).IsRequired();
        builder.Property(item => item.UpdatedAtUtc).IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();

        builder.HasIndex(item => item.ChangeNumber).IsUnique().HasDatabaseName("IX_ChangeRequest_ChangeNumber");
        builder.HasIndex(item => new { item.Status, item.UpdatedAtUtc }).HasDatabaseName("IX_ChangeRequest_Status_UpdatedAtUtc");
        builder.HasIndex(item => item.Type).HasDatabaseName("IX_ChangeRequest_Type");
        builder.HasIndex(item => item.OwnerUserId).HasDatabaseName("IX_ChangeRequest_OwnerUserId");
    }
}

internal sealed class ChangeConfigurationItemConfiguration : IEntityTypeConfiguration<ChangeConfigurationItem>
{
    public void Configure(EntityTypeBuilder<ChangeConfigurationItem> builder)
    {
        builder.ToTable("ChangeConfigurationItem");
        builder.HasKey(item => new { item.ChangeRequestId, item.ConfigurationItemId });
        builder.Property(item => item.LinkedAtUtc).IsRequired();
        builder.Property(item => item.LinkedByUserId).IsRequired();
        builder.HasIndex(item => item.ConfigurationItemId).HasDatabaseName("IX_ChangeConfigurationItem_ConfigurationItemId");
        builder.HasOne<ChangeRequest>().WithMany().HasForeignKey(item => item.ChangeRequestId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ChangeApprovalConfiguration : IEntityTypeConfiguration<ChangeApproval>
{
    public void Configure(EntityTypeBuilder<ChangeApproval> builder)
    {
        builder.ToTable("ChangeApproval");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Decision).IsRequired().HasConversion<string>().HasMaxLength(32).HasColumnType("nvarchar(32)");
        builder.Property(item => item.Comment).HasMaxLength(2000).HasColumnType("nvarchar(2000)");
        builder.Property(item => item.CreatedAtUtc).IsRequired();
        builder.HasIndex(item => new { item.ChangeRequestId, item.ApproverUserId, item.Decision })
            .HasDatabaseName("IX_ChangeApproval_Change_Approver_Decision");
        builder.HasOne<ChangeRequest>().WithMany().HasForeignKey(item => item.ChangeRequestId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ChangeStatusHistoryConfiguration : IEntityTypeConfiguration<ChangeStatusHistory>
{
    public void Configure(EntityTypeBuilder<ChangeStatusHistory> builder)
    {
        builder.ToTable("ChangeStatusHistory");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.FromStatus).IsRequired().HasConversion<string>().HasMaxLength(32).HasColumnType("nvarchar(32)");
        builder.Property(item => item.ToStatus).IsRequired().HasConversion<string>().HasMaxLength(32).HasColumnType("nvarchar(32)");
        builder.Property(item => item.Comment).HasMaxLength(2000).HasColumnType("nvarchar(2000)");
        builder.Property(item => item.ChangedAtUtc).IsRequired();
        builder.HasIndex(item => new { item.ChangeRequestId, item.ChangedAtUtc })
            .HasDatabaseName("IX_ChangeStatusHistory_Change_ChangedAt");
        builder.HasOne<ChangeRequest>().WithMany().HasForeignKey(item => item.ChangeRequestId).OnDelete(DeleteBehavior.Cascade);
    }
}
