using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qec.Itmg.Platform.Domain;

namespace Qec.Itmg.Platform.Persistence.Configurations;

public sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("Comment", PlatformDbContext.SchemaName);

        builder.HasKey(comment => comment.Id);

        builder.Property(comment => comment.Id)
            .ValueGeneratedNever();

        builder.Property(comment => comment.ResourceType)
            .IsRequired()
            .HasMaxLength(64)
            .HasColumnType("nvarchar(64)");

        builder.Property(comment => comment.ResourceId)
            .IsRequired();

        builder.Property(comment => comment.AuthorUserId)
            .IsRequired();

        builder.Property(comment => comment.Body)
            .IsRequired()
            .HasMaxLength(4000)
            .HasColumnType("nvarchar(4000)");

        builder.Property(comment => comment.Visibility)
            .IsRequired();

        builder.Property(comment => comment.CreatedAtUtc)
            .IsRequired();

        builder.Property(comment => comment.EditedAtUtc);

        builder.Property(comment => comment.RowVersion)
            .IsRowVersion();

        builder.HasIndex(comment => new { comment.ResourceType, comment.ResourceId })
            .HasDatabaseName("IX_Comment_Resource");

        builder.HasIndex(comment => comment.CreatedAtUtc)
            .HasDatabaseName("IX_Comment_CreatedAtUtc");
    }
}
