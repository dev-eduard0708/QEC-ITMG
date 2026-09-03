using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qec.Itmg.Platform.Domain;
using Qec.Itmg.Platform.Persistence;

namespace Qec.Itmg.Platform.Persistence.Configurations;

public sealed class NumberSequenceConfiguration : IEntityTypeConfiguration<Qec.Itmg.Platform.Domain.NumberSequence>
{
    public void Configure(EntityTypeBuilder<Qec.Itmg.Platform.Domain.NumberSequence> builder)
    {
        builder.ToTable("NumberSequence", PlatformDbContext.SchemaName);

        builder.HasKey(sequence => new { sequence.SequenceKey, sequence.Year });

        builder.Property(sequence => sequence.SequenceKey)
            .IsRequired()
            .HasMaxLength(64)
            .HasColumnType("nvarchar(64)");

        builder.Property(sequence => sequence.Year)
            .IsRequired();

        builder.Property(sequence => sequence.NextValue)
            .IsRequired()
            .HasColumnType("bigint");
    }
}

