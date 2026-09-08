using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qec.Itmg.Organization.Domain;

namespace Qec.Itmg.Organization.Persistence.Configurations;

internal sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("Department");

        builder.HasKey(department => department.Id);

        builder.Property(department => department.Name)
            .IsRequired()
            .HasMaxLength(128)
            .HasColumnType("nvarchar(128)");

        builder.Property(department => department.NameAr)
            .HasMaxLength(128)
            .HasColumnType("nvarchar(128)");

        builder.Property(department => department.Code)
            .IsRequired()
            .HasMaxLength(64)
            .HasColumnType("nvarchar(64)");

        builder.Property(department => department.Description)
            .HasMaxLength(512)
            .HasColumnType("nvarchar(512)");

        builder.Property(department => department.DescriptionAr)
            .HasMaxLength(512)
            .HasColumnType("nvarchar(512)");

        builder.Property(department => department.ParentDepartmentId);

        builder.Property(department => department.UnitType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasColumnType("nvarchar(32)");
        // Do not use HasDefaultValue on the enum: Company = 0 would be omitted from INSERTs
        // and the SQL default "Department" would overwrite Company roots.

        builder.Property(department => department.IsActive).IsRequired();
        builder.Property(department => department.SortOrder).IsRequired();
        builder.Property(department => department.CreatedAtUtc).IsRequired();
        builder.Property(department => department.UpdatedAtUtc).IsRequired();

        builder.Property(department => department.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasIndex(department => department.Name)
            .IsUnique()
            .HasDatabaseName("IX_Department_Name");

        builder.HasIndex(department => department.Code)
            .IsUnique()
            .HasDatabaseName("IX_Department_Code");

        builder.HasOne<Department>()
            .WithMany()
            .HasForeignKey(department => department.ParentDepartmentId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
