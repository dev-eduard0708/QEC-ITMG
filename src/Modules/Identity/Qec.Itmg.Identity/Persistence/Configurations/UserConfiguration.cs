using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qec.Itmg.Identity.Domain;

namespace Qec.Itmg.Identity.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("User");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.Upn)
            .IsRequired()
            .HasMaxLength(320)
            .HasColumnType("nvarchar(320)");

        builder.Property(user => user.DirectoryObjectId)
            .HasMaxLength(128)
            .HasColumnType("nvarchar(128)");

        builder.Property(user => user.DisplayName)
            .IsRequired()
            .HasMaxLength(256)
            .HasColumnType("nvarchar(256)");

        builder.Property(user => user.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasColumnType("nvarchar(32)");

        builder.Property(user => user.UserType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasColumnType("nvarchar(32)");

        builder.Property(user => user.TimeZone)
            .HasMaxLength(64)
            .HasColumnType("nvarchar(64)");

        builder.Property(user => user.CreatedAtUtc).IsRequired();
        builder.Property(user => user.UpdatedAtUtc).IsRequired();

        builder.Property(user => user.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasIndex(user => user.Upn)
            .IsUnique()
            .HasDatabaseName("IX_User_Upn");
    }
}
