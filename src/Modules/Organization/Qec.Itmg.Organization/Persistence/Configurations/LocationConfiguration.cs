using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qec.Itmg.Organization.Domain;

namespace Qec.Itmg.Organization.Persistence.Configurations;

internal sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable("Location");

        builder.HasKey(location => location.Id);

        builder.Property(location => location.Name)
            .IsRequired()
            .HasMaxLength(128)
            .HasColumnType("nvarchar(128)");

        builder.Property(location => location.Description)
            .HasMaxLength(512)
            .HasColumnType("nvarchar(512)");

        builder.Property(location => location.IsActive).IsRequired();
        builder.Property(location => location.CreatedAtUtc).IsRequired();
        builder.Property(location => location.UpdatedAtUtc).IsRequired();

        builder.Property(location => location.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasIndex(location => location.Name)
            .IsUnique()
            .HasDatabaseName("IX_Location_Name");
    }
}
