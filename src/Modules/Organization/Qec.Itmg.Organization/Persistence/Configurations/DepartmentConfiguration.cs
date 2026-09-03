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

        builder.Property(department => department.Description)
            .HasMaxLength(512)
            .HasColumnType("nvarchar(512)");

        builder.Property(department => department.IsActive).IsRequired();
        builder.Property(department => department.CreatedAtUtc).IsRequired();
        builder.Property(department => department.UpdatedAtUtc).IsRequired();

        builder.Property(department => department.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasIndex(department => department.Name)
            .IsUnique()
            .HasDatabaseName("IX_Department_Name");
    }
}
