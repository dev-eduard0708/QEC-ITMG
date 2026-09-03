using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qec.Itmg.ServiceDesk.Domain;

namespace Qec.Itmg.ServiceDesk.Persistence.Configurations;

internal sealed class KnowledgeArticleConfiguration : IEntityTypeConfiguration<KnowledgeArticle>
{
    public void Configure(EntityTypeBuilder<KnowledgeArticle> builder)
    {
        builder.ToTable("KnowledgeArticle");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.Title)
            .IsRequired()
            .HasMaxLength(256)
            .HasColumnType("nvarchar(256)");

        builder.Property(item => item.Slug)
            .IsRequired()
            .HasMaxLength(128)
            .HasColumnType("nvarchar(128)");

        builder.Property(item => item.Summary)
            .HasMaxLength(512)
            .HasColumnType("nvarchar(512)");

        builder.Property(item => item.Body)
            .IsRequired()
            .HasMaxLength(4000)
            .HasColumnType("nvarchar(4000)");

        builder.Property(item => item.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasColumnType("nvarchar(32)");

        builder.Property(item => item.CreatedByUserId).IsRequired();
        builder.Property(item => item.UpdatedByUserId).IsRequired();
        builder.Property(item => item.CreatedAtUtc).IsRequired();
        builder.Property(item => item.UpdatedAtUtc).IsRequired();

        builder.HasIndex(item => item.Slug)
            .IsUnique()
            .HasDatabaseName("IX_KnowledgeArticle_Slug");

        builder.HasIndex(item => new { item.Status, item.UpdatedAtUtc })
            .HasDatabaseName("IX_KnowledgeArticle_Status_UpdatedAtUtc");
    }
}
