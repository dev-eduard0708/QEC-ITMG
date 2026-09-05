using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qec.Itmg.Cmdb.Domain;

namespace Qec.Itmg.Cmdb.Persistence.Configurations;

internal sealed class CiTypeConfiguration : IEntityTypeConfiguration<CiType>
{
    public void Configure(EntityTypeBuilder<CiType> builder)
    {
        builder.ToTable("CiType");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.Key)
            .IsRequired()
            .HasMaxLength(64)
            .HasColumnType("nvarchar(64)");

        builder.Property(item => item.Name)
            .IsRequired()
            .HasMaxLength(128)
            .HasColumnType("nvarchar(128)");

        builder.Property(item => item.Description)
            .HasMaxLength(512)
            .HasColumnType("nvarchar(512)");

        builder.Property(item => item.IsActive).IsRequired();
        builder.Property(item => item.CreatedAtUtc).IsRequired();
        builder.Property(item => item.UpdatedAtUtc).IsRequired();

        builder.HasIndex(item => item.Key)
            .IsUnique()
            .HasDatabaseName("IX_CiType_Key");
    }
}

internal sealed class ConfigurationItemConfiguration : IEntityTypeConfiguration<ConfigurationItem>
{
    public void Configure(EntityTypeBuilder<ConfigurationItem> builder)
    {
        builder.ToTable("ConfigurationItem");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.CiNumber)
            .IsRequired()
            .HasMaxLength(32)
            .HasColumnType("nvarchar(32)");

        builder.Property(item => item.Name)
            .IsRequired()
            .HasMaxLength(256)
            .HasColumnType("nvarchar(256)");

        builder.Property(item => item.Description)
            .HasMaxLength(1024)
            .HasColumnType("nvarchar(1024)");

        builder.Property(item => item.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasColumnType("nvarchar(32)");

        builder.Property(item => item.Criticality)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasColumnType("nvarchar(32)");

        builder.Property(item => item.SerialNumber)
            .HasMaxLength(128)
            .HasColumnType("nvarchar(128)");

        builder.Property(item => item.Manufacturer)
            .HasMaxLength(128)
            .HasColumnType("nvarchar(128)");

        builder.Property(item => item.Model)
            .HasMaxLength(128)
            .HasColumnType("nvarchar(128)");

        builder.Property(item => item.Notes)
            .HasMaxLength(2000)
            .HasColumnType("nvarchar(2000)");

        builder.Property(item => item.SpofReason).HasMaxLength(2000);
        builder.Property(item => item.SpofMitigationNotes).HasMaxLength(2000);

        builder.Property(item => item.CreatedAtUtc).IsRequired();
        builder.Property(item => item.UpdatedAtUtc).IsRequired();
        builder.Property(item => item.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasIndex(item => item.CiNumber)
            .IsUnique()
            .HasDatabaseName("IX_ConfigurationItem_CiNumber");

        builder.HasIndex(item => item.CiTypeId)
            .HasDatabaseName("IX_ConfigurationItem_CiTypeId");

        builder.HasIndex(item => item.IsSinglePointOfFailure)
            .HasDatabaseName("IX_ConfigurationItem_IsSinglePointOfFailure");

        builder.HasIndex(item => item.VendorId)
            .HasDatabaseName("IX_ConfigurationItem_VendorId");

        builder.HasOne<CiType>()
            .WithMany()
            .HasForeignKey(item => item.CiTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
