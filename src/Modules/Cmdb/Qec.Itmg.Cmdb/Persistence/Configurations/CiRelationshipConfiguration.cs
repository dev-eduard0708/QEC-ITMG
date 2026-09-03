using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qec.Itmg.Cmdb.Domain;

namespace Qec.Itmg.Cmdb.Persistence.Configurations;

internal sealed class CiRelationshipConfiguration : IEntityTypeConfiguration<CiRelationship>
{
    public void Configure(EntityTypeBuilder<CiRelationship> builder)
    {
        builder.ToTable("CiRelationship");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.RelationshipType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasColumnType("nvarchar(32)");

        builder.Property(item => item.Notes)
            .HasMaxLength(1024)
            .HasColumnType("nvarchar(1024)");

        builder.Property(item => item.CreatedAtUtc).IsRequired();

        builder.HasIndex(item => new { item.SourceCiId, item.TargetCiId, item.RelationshipType })
            .IsUnique()
            .HasDatabaseName("IX_CiRelationship_Source_Target_Type");

        builder.HasOne<ConfigurationItem>()
            .WithMany()
            .HasForeignKey(item => item.SourceCiId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ConfigurationItem>()
            .WithMany()
            .HasForeignKey(item => item.TargetCiId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
