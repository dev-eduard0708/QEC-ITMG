using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qec.Itmg.ThirdParty.Domain;

namespace Qec.Itmg.ThirdParty.Persistence.Configurations;

internal sealed class VendorConfiguration : IEntityTypeConfiguration<Vendor>
{
    public void Configure(EntityTypeBuilder<Vendor> builder)
    {
        builder.ToTable("Vendor");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.VendorNumber).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(256);
        builder.Property(x => x.LegalName).HasMaxLength(512);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Criticality).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.ServiceDescription).HasMaxLength(4000);
        builder.Property(x => x.PrimaryContactName).HasMaxLength(256);
        builder.Property(x => x.PrimaryContactEmail).HasMaxLength(320);
        builder.Property(x => x.PrimaryContactPhone).HasMaxLength(64);
        builder.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(x => x.VendorNumber).IsUnique().HasDatabaseName("IX_Vendor_Number");
        builder.HasIndex(x => new { x.Name, x.Status }).HasDatabaseName("IX_Vendor_Name_Status");
        builder.HasIndex(x => x.Criticality).HasDatabaseName("IX_Vendor_Criticality");
    }
}

internal sealed class VendorContactConfiguration : IEntityTypeConfiguration<VendorContact>
{
    public void Configure(EntityTypeBuilder<VendorContact> builder)
    {
        builder.ToTable("VendorContact");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Email).HasMaxLength(320);
        builder.Property(x => x.Phone).HasMaxLength(64);
        builder.Property(x => x.Role).HasMaxLength(128);
        builder.HasIndex(x => new { x.VendorId, x.Email }).HasDatabaseName("IX_VendorContact_Vendor_Email");
        builder.HasOne<Vendor>().WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        builder.ToTable("Contract");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ContractNumber).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(512);
        builder.Property(x => x.ContractType).HasMaxLength(128);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.SlaReference).HasMaxLength(512);
        builder.Property(x => x.Notes).HasMaxLength(4000);
        builder.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(x => x.ContractNumber).IsUnique().HasDatabaseName("IX_Contract_Number");
        builder.HasIndex(x => new { x.VendorId, x.Status }).HasDatabaseName("IX_Contract_Vendor_Status");
        builder.HasIndex(x => x.EndDate).HasDatabaseName("IX_Contract_EndDate");
        builder.HasIndex(x => x.RenewalDate).HasDatabaseName("IX_Contract_RenewalDate");
        builder.HasOne<Vendor>().WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class VendorAssessmentConfiguration : IEntityTypeConfiguration<VendorAssessment>
{
    public void Configure(EntityTypeBuilder<VendorAssessment> builder)
    {
        builder.ToTable("VendorAssessment");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AssessmentNumber).IsRequired().HasMaxLength(32);
        builder.Property(x => x.AssessmentType).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Result).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Summary).HasMaxLength(8000);
        builder.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(x => x.AssessmentNumber).IsUnique().HasDatabaseName("IX_VendorAssessment_Number");
        builder.HasIndex(x => new { x.VendorId, x.Status }).HasDatabaseName("IX_VendorAssessment_Vendor_Status");
        builder.HasIndex(x => x.DueAtUtc).HasDatabaseName("IX_VendorAssessment_DueAtUtc");
        builder.HasOne<Vendor>().WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class VendorScopeLinkConfiguration : IEntityTypeConfiguration<VendorScopeLink>
{
    public void Configure(EntityTypeBuilder<VendorScopeLink> builder)
    {
        builder.ToTable("VendorScopeLink");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TargetType).IsRequired().HasConversion<string>().HasMaxLength(64);
        builder.HasIndex(x => new { x.VendorId, x.TargetType, x.TargetId })
            .IsUnique()
            .HasDatabaseName("IX_VendorScopeLink_Unique");
        builder.HasOne<Vendor>().WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class VendorNotificationLogConfiguration : IEntityTypeConfiguration<VendorNotificationLog>
{
    public void Configure(EntityTypeBuilder<VendorNotificationLog> builder)
    {
        builder.ToTable("VendorNotificationLog");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventKey).IsRequired().HasMaxLength(96);
        builder.HasIndex(x => new { x.ResourceId, x.EventKey })
            .IsUnique()
            .HasDatabaseName("IX_VendorNotificationLog_Unique");
    }
}
