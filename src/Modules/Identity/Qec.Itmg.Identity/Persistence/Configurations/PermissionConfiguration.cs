using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qec.Itmg.Identity.Domain;

namespace Qec.Itmg.Identity.Persistence.Configurations;

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permission");

        builder.HasKey(permission => permission.Id);

        builder.Property(permission => permission.Key)
            .IsRequired()
            .HasMaxLength(128)
            .HasColumnType("nvarchar(128)");

        builder.Property(permission => permission.Description)
            .HasMaxLength(512)
            .HasColumnType("nvarchar(512)");

        builder.HasIndex(permission => permission.Key)
            .IsUnique()
            .HasDatabaseName("IX_Permission_Key");
    }
}
