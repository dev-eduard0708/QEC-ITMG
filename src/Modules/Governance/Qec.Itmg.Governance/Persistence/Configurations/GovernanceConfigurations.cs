using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qec.Itmg.Governance.Domain;

namespace Qec.Itmg.Governance.Persistence.Configurations;

internal sealed class OrganizationProfileConfiguration : IEntityTypeConfiguration<OrganizationProfile>
{
    public void Configure(EntityTypeBuilder<OrganizationProfile> builder)
    {
        builder.ToTable("OrganizationProfile");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.LegalName).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Timezone).IsRequired().HasMaxLength(64);
        builder.Property(x => x.ClassificationScheme).HasMaxLength(128);
        builder.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
    }
}

internal sealed class OrganizationalUnitConfiguration : IEntityTypeConfiguration<OrganizationalUnit>
{
    public void Configure(EntityTypeBuilder<OrganizationalUnit> builder)
    {
        builder.ToTable("OrganizationalUnit");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Code).HasMaxLength(64);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(x => x.ParentId).HasDatabaseName("IX_OrganizationalUnit_ParentId");
        builder.HasIndex(x => x.ManagerUserId).HasDatabaseName("IX_OrganizationalUnit_ManagerUserId");
        builder.HasIndex(x => x.Code).HasDatabaseName("IX_OrganizationalUnit_Code");
        builder.HasOne<OrganizationalUnit>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class OrganizationalUnitMembershipConfiguration : IEntityTypeConfiguration<OrganizationalUnitMembership>
{
    public void Configure(EntityTypeBuilder<OrganizationalUnitMembership> builder)
    {
        builder.ToTable("OrganizationalUnitMembership");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.OrganizationalUnitId, x.UserId })
            .IsUnique()
            .HasDatabaseName("IX_OrganizationalUnitMembership_Unit_User");
        builder.HasOne<OrganizationalUnit>().WithMany().HasForeignKey(x => x.OrganizationalUnitId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class InternalControlConfiguration : IEntityTypeConfiguration<InternalControl>
{
    public void Configure(EntityTypeBuilder<InternalControl> builder)
    {
        builder.ToTable("InternalControl");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ControlNumber).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(512);
        builder.Property(x => x.Objective).IsRequired().HasMaxLength(4000);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(8000);
        builder.Property(x => x.Domain).IsRequired().HasMaxLength(16);
        builder.Property(x => x.Frequency).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.AutomationType).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(x => x.ControlNumber).IsUnique().HasDatabaseName("IX_InternalControl_ControlNumber");
        builder.HasIndex(x => x.Domain).HasDatabaseName("IX_InternalControl_Domain");
        builder.HasIndex(x => x.Status).HasDatabaseName("IX_InternalControl_Status");
        builder.HasIndex(x => x.PrimaryOwnerUserId).HasDatabaseName("IX_InternalControl_PrimaryOwnerUserId");
    }
}

internal sealed class ControlSecondaryOwnerConfiguration : IEntityTypeConfiguration<ControlSecondaryOwner>
{
    public void Configure(EntityTypeBuilder<ControlSecondaryOwner> builder)
    {
        builder.ToTable("ControlSecondaryOwner");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.InternalControlId, x.UserId })
            .IsUnique()
            .HasDatabaseName("IX_ControlSecondaryOwner_Control_User");
        builder.HasOne<InternalControl>().WithMany().HasForeignKey(x => x.InternalControlId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ControlConfigurationItemLinkConfiguration : IEntityTypeConfiguration<ControlConfigurationItemLink>
{
    public void Configure(EntityTypeBuilder<ControlConfigurationItemLink> builder)
    {
        builder.ToTable("ControlConfigurationItemLink");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.InternalControlId, x.ConfigurationItemId })
            .IsUnique()
            .HasDatabaseName("IX_ControlCiLink_Control_Ci");
        builder.HasOne<InternalControl>().WithMany().HasForeignKey(x => x.InternalControlId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ControlBusinessServiceLinkConfiguration : IEntityTypeConfiguration<ControlBusinessServiceLink>
{
    public void Configure(EntityTypeBuilder<ControlBusinessServiceLink> builder)
    {
        builder.ToTable("ControlBusinessServiceLink");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.InternalControlId, x.BusinessServiceId })
            .IsUnique()
            .HasDatabaseName("IX_ControlServiceLink_Control_Service");
        builder.HasOne<InternalControl>().WithMany().HasForeignKey(x => x.InternalControlId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ControlManagedDocumentLinkConfiguration : IEntityTypeConfiguration<ControlManagedDocumentLink>
{
    public void Configure(EntityTypeBuilder<ControlManagedDocumentLink> builder)
    {
        builder.ToTable("ControlManagedDocumentLink");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.InternalControlId, x.ManagedDocumentId })
            .IsUnique()
            .HasDatabaseName("IX_ControlDocumentLink_Control_Doc");
        builder.HasOne<InternalControl>().WithMany().HasForeignKey(x => x.InternalControlId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ControlTestProcedureConfiguration : IEntityTypeConfiguration<ControlTestProcedure>
{
    public void Configure(EntityTypeBuilder<ControlTestProcedure> builder)
    {
        builder.ToTable("ControlTestProcedure");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(512);
        builder.Property(x => x.Purpose).HasMaxLength(2000);
        builder.Property(x => x.ProcedureSteps).IsRequired().HasMaxLength(8000);
        builder.Property(x => x.ExpectedResult).IsRequired().HasMaxLength(4000);
        builder.Property(x => x.SampleGuidance).HasMaxLength(4000);
        builder.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(x => x.InternalControlId).HasDatabaseName("IX_ControlTestProcedure_InternalControlId");
        builder.HasOne<InternalControl>().WithMany().HasForeignKey(x => x.InternalControlId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class EvidenceRequirementConfiguration : IEntityTypeConfiguration<EvidenceRequirement>
{
    public void Configure(EntityTypeBuilder<EvidenceRequirement> builder)
    {
        builder.ToTable("EvidenceRequirement");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.Frequency).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.RetentionNotes).HasMaxLength(1000);
        builder.HasIndex(x => x.InternalControlId).HasDatabaseName("IX_EvidenceRequirement_InternalControlId");
        builder.HasOne<InternalControl>().WithMany().HasForeignKey(x => x.InternalControlId).OnDelete(DeleteBehavior.Cascade);
    }
}
