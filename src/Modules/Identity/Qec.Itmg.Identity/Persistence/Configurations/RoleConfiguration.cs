using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qec.Itmg.Identity.Domain;

namespace Qec.Itmg.Identity.Persistence.Configurations;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Role");

        builder.HasKey(role => role.Id);

        builder.Property(role => role.Name)
            .IsRequired()
            .HasMaxLength(128)
            .HasColumnType("nvarchar(128)");

        builder.Property(role => role.Description)
            .HasMaxLength(512)
            .HasColumnType("nvarchar(512)");

        builder.Property(role => role.IsSystem).IsRequired();
        builder.Property(role => role.CreatedAtUtc).IsRequired();
        builder.Property(role => role.UpdatedAtUtc).IsRequired();

        builder.Property(role => role.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasIndex(role => role.Name)
            .IsUnique()
            .HasDatabaseName("IX_Role_Name");
    }
}
