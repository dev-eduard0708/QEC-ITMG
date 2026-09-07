using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qec.Itmg.Organization.Domain;

namespace Qec.Itmg.Organization.Persistence.Configurations;

internal sealed class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> builder)
    {
        builder.ToTable("Position");

        builder.HasKey(position => position.Id);

        builder.Property(position => position.DepartmentId).IsRequired();
        builder.Property(position => position.ParentPositionId);

        builder.Property(position => position.Key)
            .IsRequired()
            .HasMaxLength(64)
            .HasColumnType("nvarchar(64)");

        builder.Property(position => position.NameEn)
            .IsRequired()
            .HasMaxLength(256)
            .HasColumnType("nvarchar(256)");

        builder.Property(position => position.NameAr)
            .IsRequired()
            .HasMaxLength(256)
            .HasColumnType("nvarchar(256)");

        builder.Property(position => position.DescriptionEn)
            .HasMaxLength(2000)
            .HasColumnType("nvarchar(2000)");

        builder.Property(position => position.DescriptionAr)
            .HasMaxLength(2000)
            .HasColumnType("nvarchar(2000)");

        builder.Property(position => position.IsManagerial).IsRequired();
        builder.Property(position => position.IsActive).IsRequired();
        builder.Property(position => position.SortOrder).IsRequired();
        builder.Property(position => position.CreatedAtUtc).IsRequired();
        builder.Property(position => position.UpdatedAtUtc).IsRequired();

        builder.Property(position => position.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasIndex(position => new { position.DepartmentId, position.Key })
            .IsUnique()
            .HasDatabaseName("IX_Position_DepartmentId_Key");

        builder.HasIndex(position => position.ParentPositionId)
            .HasDatabaseName("IX_Position_ParentPositionId");

        builder.HasIndex(position => position.DepartmentId)
            .HasDatabaseName("IX_Position_DepartmentId");

        builder.HasOne<Department>()
            .WithMany()
            .HasForeignKey(position => position.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Position>()
            .WithMany()
            .HasForeignKey(position => position.ParentPositionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
