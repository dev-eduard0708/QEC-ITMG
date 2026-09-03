using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qec.Itmg.Platform.Domain;
using Qec.Itmg.Platform.Persistence;

namespace Qec.Itmg.Platform.Persistence.Configurations;

public sealed class AttachmentMetadataConfiguration : IEntityTypeConfiguration<AttachmentMetadata>
{
    public void Configure(EntityTypeBuilder<AttachmentMetadata> builder)
    {
        builder.ToTable("AttachmentMetadata", PlatformDbContext.SchemaName);

        builder.HasKey(candidate => candidate.Id);

        builder.Property(candidate => candidate.Id)
            .ValueGeneratedNever();

        builder.Property(candidate => candidate.OriginalFileName)
            .IsRequired()
            .HasMaxLength(256)
            .HasColumnType("nvarchar(256)");

        builder.Property(candidate => candidate.StorageKey)
            .IsRequired()
            .HasMaxLength(128)
            .HasColumnType("nvarchar(128)");

        builder.Property(candidate => candidate.ContentType)
            .IsRequired()
            .HasMaxLength(128)
            .HasColumnType("nvarchar(128)");

        builder.Property(candidate => candidate.SizeBytes)
            .IsRequired()
            .HasColumnType("bigint");

        builder.Property(candidate => candidate.Sha256)
            .IsRequired()
            .HasMaxLength(64)
            .HasColumnType("nvarchar(64)");

        builder.Property(candidate => candidate.UploadedByUserId)
            .IsRequired();

        builder.Property(candidate => candidate.UploadedAtUtc)
            .IsRequired();

        builder.Property(candidate => candidate.ScanStatus)
            .IsRequired();

        builder.Property(candidate => candidate.ScanProvider)
            .HasMaxLength(64)
            .HasColumnType("nvarchar(64)");

        builder.Property(candidate => candidate.ScanMessage)
            .HasMaxLength(2048)
            .HasColumnType("nvarchar(2048)");

        builder.Property(candidate => candidate.ScannedAtUtc)
            .HasColumnType("datetimeoffset");

        builder.Property(candidate => candidate.RowVersion)
            .IsRowVersion();
    }
}

