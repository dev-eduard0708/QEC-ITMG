using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qec.Itmg.Platform.Domain;

namespace Qec.Itmg.Platform.Persistence.Configurations;

internal sealed class BusinessAuditRecordConfiguration : IEntityTypeConfiguration<BusinessAuditRecord>
{
    public void Configure(EntityTypeBuilder<BusinessAuditRecord> builder)
    {
        builder.ToTable("BusinessAuditRecord");

        builder.HasKey(record => record.Id);

        builder.Property(record => record.AggregateType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(64)
            .HasColumnType("nvarchar(64)");

        builder.Property(record => record.AggregateId).IsRequired();

        builder.Property(record => record.BusinessNumber)
            .HasMaxLength(64)
            .HasColumnType("nvarchar(64)");

        builder.Property(record => record.OccurredAtUtc).IsRequired();

        builder.Property(record => record.ActorType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasColumnType("nvarchar(32)");

        builder.Property(record => record.JobName)
            .HasMaxLength(128)
            .HasColumnType("nvarchar(128)");

        builder.Property(record => record.Source)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasColumnType("nvarchar(32)");

        builder.Property(record => record.Action)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(64)
            .HasColumnType("nvarchar(64)");

        builder.Property(record => record.FieldName)
            .HasMaxLength(128)
            .HasColumnType("nvarchar(128)");

        builder.Property(record => record.OldValue)
            .HasMaxLength(4000)
            .HasColumnType("nvarchar(4000)");

        builder.Property(record => record.NewValue)
            .HasMaxLength(4000)
            .HasColumnType("nvarchar(4000)");

        builder.Property(record => record.Reason)
            .HasMaxLength(1024)
            .HasColumnType("nvarchar(1024)");

        builder.Property(record => record.CorrelationId)
            .HasMaxLength(128)
            .HasColumnType("nvarchar(128)");

        builder.Property(record => record.ClientIp)
            .HasMaxLength(64)
            .HasColumnType("nvarchar(64)");

        builder.HasIndex(record => new { record.AggregateType, record.AggregateId })
            .HasDatabaseName("IX_BusinessAuditRecord_Aggregate");

        builder.HasIndex(record => record.OccurredAtUtc)
            .HasDatabaseName("IX_BusinessAuditRecord_OccurredAtUtc");

        builder.HasIndex(record => record.ActorUserId)
            .HasDatabaseName("IX_BusinessAuditRecord_ActorUserId");

        builder.HasIndex(record => record.CorrelationId)
            .HasDatabaseName("IX_BusinessAuditRecord_CorrelationId");
    }
}
