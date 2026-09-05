using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qec.Itmg.Ai.Domain;

namespace Qec.Itmg.Ai.Persistence.Configurations;

public sealed class AiInteractionConfiguration : IEntityTypeConfiguration<AiInteraction>
{
    public void Configure(EntityTypeBuilder<AiInteraction> builder)
    {
        builder.ToTable("AiInteraction", AiDbContext.SchemaName);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CorrelationId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Capability).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Provider).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ModelName).HasMaxLength(128);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.ClassificationContext).HasMaxLength(64);
        builder.Property(x => x.ErrorSummary).HasMaxLength(1000);
        builder.HasIndex(x => new { x.UserId, x.StartedAtUtc });
        builder.HasIndex(x => x.CorrelationId);
        builder.HasIndex(x => new { x.Capability, x.Status });
        builder.HasMany(x => x.ToolInvocations).WithOne().HasForeignKey(x => x.InteractionId);
    }
}

public sealed class AiToolInvocationConfiguration : IEntityTypeConfiguration<AiToolInvocation>
{
    public void Configure(EntityTypeBuilder<AiToolInvocation> builder)
    {
        builder.ToTable("AiToolInvocation", AiDbContext.SchemaName);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ToolName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.RecordType).HasMaxLength(64);
        builder.Property(x => x.Result).HasMaxLength(256).IsRequired();
        builder.HasIndex(x => new { x.InteractionId, x.ToolName });
    }
}
