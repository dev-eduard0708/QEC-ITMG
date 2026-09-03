using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qec.Itmg.Platform.Domain;

namespace Qec.Itmg.Platform.Persistence.Configurations;

internal sealed class SecurityAuditEventConfiguration : IEntityTypeConfiguration<SecurityAuditEvent>
{
    public void Configure(EntityTypeBuilder<SecurityAuditEvent> builder)
    {
        builder.ToTable("SecurityAuditEvent");

        builder.HasKey(record => record.Id);

        builder.Property(record => record.OccurredAtUtc).IsRequired();

        builder.Property(record => record.EventType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(64)
            .HasColumnType("nvarchar(64)");

        builder.Property(record => record.Outcome)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasColumnType("nvarchar(32)");

        builder.Property(record => record.TargetType)
            .HasMaxLength(64)
            .HasColumnType("nvarchar(64)");

        builder.Property(record => record.TargetId)
            .HasMaxLength(128)
            .HasColumnType("nvarchar(128)");

        builder.Property(record => record.Details)
            .HasMaxLength(4000)
            .HasColumnType("nvarchar(4000)");

        builder.Property(record => record.CorrelationId)
            .HasMaxLength(128)
            .HasColumnType("nvarchar(128)");

        builder.Property(record => record.ClientIp)
            .HasMaxLength(64)
            .HasColumnType("nvarchar(64)");

        builder.HasIndex(record => record.OccurredAtUtc)
            .HasDatabaseName("IX_SecurityAuditEvent_OccurredAtUtc");

        builder.HasIndex(record => record.ActorUserId)
            .HasDatabaseName("IX_SecurityAuditEvent_ActorUserId");

        builder.HasIndex(record => record.CorrelationId)
            .HasDatabaseName("IX_SecurityAuditEvent_CorrelationId");

        builder.HasIndex(record => new { record.EventType, record.OccurredAtUtc })
            .HasDatabaseName("IX_SecurityAuditEvent_EventType_OccurredAtUtc");
    }
}
