using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qec.Itmg.Cmdb.Domain;

namespace Qec.Itmg.Cmdb.Persistence.Configurations;

internal sealed class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.ToTable("Asset");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.AssetNumber)
            .IsRequired()
            .HasMaxLength(32)
            .HasColumnType("nvarchar(32)");

        builder.Property(item => item.AssetType)
            .IsRequired()
            .HasMaxLength(64)
            .HasColumnType("nvarchar(64)");

        builder.Property(item => item.Name)
            .IsRequired()
            .HasMaxLength(256)
            .HasColumnType("nvarchar(256)");

        builder.Property(item => item.SerialNumber).HasMaxLength(128).HasColumnType("nvarchar(128)");
        builder.Property(item => item.Manufacturer).HasMaxLength(128).HasColumnType("nvarchar(128)");
        builder.Property(item => item.Model).HasMaxLength(128).HasColumnType("nvarchar(128)");
        builder.Property(item => item.Notes).HasMaxLength(2000).HasColumnType("nvarchar(2000)");

        builder.Property(item => item.PurchaseCost).HasColumnType("decimal(18,2)");

        builder.Property(item => item.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasColumnType("nvarchar(32)");

        builder.Property(item => item.CreatedAtUtc).IsRequired();
        builder.Property(item => item.UpdatedAtUtc).IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();

        builder.HasIndex(item => item.AssetNumber)
            .IsUnique()
            .HasDatabaseName("IX_Asset_AssetNumber");

        builder.HasOne<ConfigurationItem>()
            .WithMany()
            .HasForeignKey(item => item.ConfigurationItemId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class AssetAssignmentConfiguration : IEntityTypeConfiguration<AssetAssignment>
{
    public void Configure(EntityTypeBuilder<AssetAssignment> builder)
    {
        builder.ToTable("AssetAssignment");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.Notes).HasMaxLength(1024).HasColumnType("nvarchar(1024)");
        builder.Property(item => item.AssignedAtUtc).IsRequired();

        builder.HasIndex(item => item.AssetId)
            .IsUnique()
            .HasFilter("[ReturnedAtUtc] IS NULL")
            .HasDatabaseName("IX_AssetAssignment_AssetId_Active");

        builder.HasOne<Asset>()
            .WithMany()
            .HasForeignKey(item => item.AssetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
