using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qec.Itmg.Organization.Domain;

namespace Qec.Itmg.Organization.Persistence.Configurations;

internal sealed class PositionAssignmentConfiguration : IEntityTypeConfiguration<PositionAssignment>
{
    public void Configure(EntityTypeBuilder<PositionAssignment> builder)
    {
        builder.ToTable("PositionAssignment");

        builder.HasKey(assignment => assignment.Id);

        builder.Property(assignment => assignment.PositionId).IsRequired();
        builder.Property(assignment => assignment.UserId).IsRequired();
        builder.Property(assignment => assignment.IsPrimary).IsRequired();
        builder.Property(assignment => assignment.EffectiveFrom);
        builder.Property(assignment => assignment.EffectiveTo);
        builder.Property(assignment => assignment.CreatedAtUtc).IsRequired();
        builder.Property(assignment => assignment.UpdatedAtUtc).IsRequired();

        builder.HasIndex(assignment => new { assignment.PositionId, assignment.UserId })
            .IsUnique()
            .HasDatabaseName("IX_PositionAssignment_PositionId_UserId");

        builder.HasIndex(assignment => assignment.UserId)
            .HasDatabaseName("IX_PositionAssignment_UserId");

        builder.HasOne<Position>()
            .WithMany()
            .HasForeignKey(assignment => assignment.PositionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
