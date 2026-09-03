using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qec.Itmg.Cmdb.Domain;

namespace Qec.Itmg.Cmdb.Persistence.Configurations;

internal sealed class BusinessServiceConfiguration : IEntityTypeConfiguration<BusinessService>
{
    public void Configure(EntityTypeBuilder<BusinessService> builder)
    {
        builder.ToTable("BusinessService");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.Name)
            .IsRequired()
            .HasMaxLength(256)
            .HasColumnType("nvarchar(256)");

        builder.Property(item => item.Description)
            .HasMaxLength(1024)
            .HasColumnType("nvarchar(1024)");

        builder.Property(item => item.Criticality)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasColumnType("nvarchar(32)");

        builder.Property(item => item.IsActive).IsRequired();
        builder.Property(item => item.CreatedAtUtc).IsRequired();
        builder.Property(item => item.UpdatedAtUtc).IsRequired();

        builder.HasIndex(item => item.Name)
            .IsUnique()
            .HasDatabaseName("IX_BusinessService_Name");
    }
}

internal sealed class BusinessServiceConfigurationItemConfiguration
    : IEntityTypeConfiguration<BusinessServiceConfigurationItem>
{
    public void Configure(EntityTypeBuilder<BusinessServiceConfigurationItem> builder)
    {
        builder.ToTable("BusinessServiceConfigurationItem");
        builder.HasKey(item => new { item.BusinessServiceId, item.ConfigurationItemId });

        builder.Property(item => item.LinkedAtUtc).IsRequired();

        builder.HasOne<BusinessService>()
            .WithMany()
            .HasForeignKey(item => item.BusinessServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ConfigurationItem>()
            .WithMany()
            .HasForeignKey(item => item.ConfigurationItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
