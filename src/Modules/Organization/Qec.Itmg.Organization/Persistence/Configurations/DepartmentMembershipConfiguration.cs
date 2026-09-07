using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qec.Itmg.Organization.Domain;

namespace Qec.Itmg.Organization.Persistence.Configurations;

internal sealed class DepartmentMembershipConfiguration : IEntityTypeConfiguration<DepartmentMembership>
{
    public void Configure(EntityTypeBuilder<DepartmentMembership> builder)
    {
        builder.ToTable("DepartmentMembership");

        builder.HasKey(membership => membership.Id);

        builder.Property(membership => membership.DepartmentId).IsRequired();
        builder.Property(membership => membership.UserId).IsRequired();
        builder.Property(membership => membership.IsPrimary).IsRequired();
        builder.Property(membership => membership.EffectiveFrom);
        builder.Property(membership => membership.EffectiveTo);
        builder.Property(membership => membership.CreatedAtUtc).IsRequired();
        builder.Property(membership => membership.UpdatedAtUtc).IsRequired();

        builder.HasOne<Department>()
            .WithMany()
            .HasForeignKey(membership => membership.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(membership => new { membership.DepartmentId, membership.UserId })
            .IsUnique()
            .HasFilter("[EffectiveTo] IS NULL")
            .HasDatabaseName("IX_DepartmentMembership_Department_User_Active");

        builder.HasIndex(membership => membership.UserId)
            .HasDatabaseName("IX_DepartmentMembership_UserId");
    }
}
