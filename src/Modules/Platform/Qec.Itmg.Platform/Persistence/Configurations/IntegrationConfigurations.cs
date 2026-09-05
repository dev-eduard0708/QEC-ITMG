using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qec.Itmg.Platform.Domain;

namespace Qec.Itmg.Platform.Persistence.Configurations;

public sealed class IntegrationRunConfiguration : IEntityTypeConfiguration<IntegrationRun>
{
    public void Configure(EntityTypeBuilder<IntegrationRun> builder)
    {
        builder.ToTable("IntegrationRun", PlatformDbContext.SchemaName);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Provider).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Operation).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.ErrorSummary).HasMaxLength(2000);
        builder.Property(x => x.CorrelationId).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.Provider, x.StartedAtUtc });
        builder.HasIndex(x => x.CorrelationId);
    }
}

public sealed class IntegrationWebhookReceiptConfiguration : IEntityTypeConfiguration<IntegrationWebhookReceipt>
{
    public void Configure(EntityTypeBuilder<IntegrationWebhookReceipt> builder)
    {
        builder.ToTable("IntegrationWebhookReceipt", PlatformDbContext.SchemaName);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Provider).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ExternalEventId).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Result).HasMaxLength(64).IsRequired();
        builder.Property(x => x.PayloadHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ErrorSummary).HasMaxLength(1000);
        builder.HasIndex(x => new { x.Provider, x.ExternalEventId }).IsUnique();
        builder.HasIndex(x => x.ReceivedAtUtc);
    }
}

public sealed class IntegrationCorrelationConfiguration : IEntityTypeConfiguration<IntegrationCorrelation>
{
    public void Configure(EntityTypeBuilder<IntegrationCorrelation> builder)
    {
        builder.ToTable("IntegrationCorrelation", PlatformDbContext.SchemaName);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Provider).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ExternalId).HasMaxLength(256).IsRequired();
        builder.Property(x => x.TargetType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(256);
        builder.Property(x => x.MatchStatus).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.Provider, x.ExternalId, x.TargetType }).IsUnique();
        builder.HasIndex(x => new { x.MatchStatus, x.Provider });
    }
}
