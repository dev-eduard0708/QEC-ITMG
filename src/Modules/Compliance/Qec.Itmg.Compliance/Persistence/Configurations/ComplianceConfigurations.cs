using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qec.Itmg.Compliance.Domain;

namespace Qec.Itmg.Compliance.Persistence.Configurations;

internal sealed class FrameworkConfiguration : IEntityTypeConfiguration<Framework>
{
    public void Configure(EntityTypeBuilder<Framework> builder)
    {
        builder.ToTable("Framework");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Publisher).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("IX_Framework_Code");
    }
}

internal sealed class FrameworkVersionConfiguration : IEntityTypeConfiguration<FrameworkVersion>
{
    public void Configure(EntityTypeBuilder<FrameworkVersion> builder)
    {
        builder.ToTable("FrameworkVersion");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.VersionCode).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Title).HasMaxLength(256);
        builder.HasIndex(x => new { x.FrameworkId, x.VersionCode })
            .IsUnique()
            .HasDatabaseName("IX_FrameworkVersion_Framework_Version");
        builder.HasOne<Framework>().WithMany().HasForeignKey(x => x.FrameworkId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class FrameworkRequirementConfiguration : IEntityTypeConfiguration<FrameworkRequirement>
{
    public void Configure(EntityTypeBuilder<FrameworkRequirement> builder)
    {
        builder.ToTable("FrameworkRequirement");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(512);
        builder.Property(x => x.Text).HasMaxLength(8000);
        builder.Property(x => x.RequirementType).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(x => new { x.FrameworkVersionId, x.Code })
            .IsUnique()
            .HasDatabaseName("IX_FrameworkRequirement_Version_Code");
        builder.HasIndex(x => x.ParentRequirementId).HasDatabaseName("IX_FrameworkRequirement_Parent");
        builder.HasOne<FrameworkVersion>().WithMany().HasForeignKey(x => x.FrameworkVersionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<FrameworkRequirement>().WithMany().HasForeignKey(x => x.ParentRequirementId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ControlMappingConfiguration : IEntityTypeConfiguration<ControlMapping>
{
    public void Configure(EntityTypeBuilder<ControlMapping> builder)
    {
        builder.ToTable("ControlMapping");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Relationship).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.HasIndex(x => new { x.InternalControlId, x.FrameworkRequirementId })
            .IsUnique()
            .HasDatabaseName("IX_ControlMapping_Control_Requirement");
        builder.HasOne<FrameworkRequirement>().WithMany().HasForeignKey(x => x.FrameworkRequirementId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ControlAssessmentConfiguration : IEntityTypeConfiguration<ControlAssessment>
{
    public void Configure(EntityTypeBuilder<ControlAssessment> builder)
    {
        builder.ToTable("ControlAssessment");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Result).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Notes).HasMaxLength(4000);
        builder.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(x => new { x.InternalControlId, x.Status }).HasDatabaseName("IX_ControlAssessment_Control_Status");
        builder.HasIndex(x => x.AssessmentDateUtc).HasDatabaseName("IX_ControlAssessment_AssessmentDate");
        builder.HasIndex(x => x.FrameworkVersionId).HasDatabaseName("IX_ControlAssessment_FrameworkVersion");
    }
}

internal sealed class ComplianceCalendarItemConfiguration : IEntityTypeConfiguration<ComplianceCalendarItem>
{
    public void Configure(EntityTypeBuilder<ComplianceCalendarItem> builder)
    {
        builder.ToTable("ComplianceCalendarItem");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(512);
        builder.Property(x => x.ItemType).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.HasIndex(x => x.DueAtUtc).HasDatabaseName("IX_ComplianceCalendarItem_DueAt");
        builder.HasIndex(x => x.Status).HasDatabaseName("IX_ComplianceCalendarItem_Status");
        builder.HasIndex(x => x.OwnerUserId).HasDatabaseName("IX_ComplianceCalendarItem_Owner");
    }
}
