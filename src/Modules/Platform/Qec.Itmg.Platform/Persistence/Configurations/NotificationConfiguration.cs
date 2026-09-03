using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qec.Itmg.Platform.Domain;

namespace Qec.Itmg.Platform.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notification", PlatformDbContext.SchemaName);
        builder.HasKey(notification => notification.Id);
        builder.Property(notification => notification.Id).ValueGeneratedNever();

        builder.Property(notification => notification.RecipientUserId).IsRequired();

        builder.Property(notification => notification.Type)
            .IsRequired()
            .HasMaxLength(64)
            .HasColumnType("nvarchar(64)");

        builder.Property(notification => notification.Severity).IsRequired();

        builder.Property(notification => notification.Title)
            .IsRequired()
            .HasMaxLength(256)
            .HasColumnType("nvarchar(256)");

        builder.Property(notification => notification.Message)
            .IsRequired()
            .HasMaxLength(2000)
            .HasColumnType("nvarchar(2000)");

        builder.Property(notification => notification.ResourceType)
            .HasMaxLength(64)
            .HasColumnType("nvarchar(64)");

        builder.Property(notification => notification.ActionUrl)
            .HasMaxLength(512)
            .HasColumnType("nvarchar(512)");

        builder.Property(notification => notification.CreatedAtUtc).IsRequired();

        builder.Ignore(notification => notification.IsRead);

        builder.HasIndex(notification => new { notification.RecipientUserId, notification.CreatedAtUtc })
            .HasDatabaseName("IX_Notification_Recipient_CreatedAt");

        builder.HasIndex(notification => new { notification.RecipientUserId, notification.ReadAtUtc })
            .HasDatabaseName("IX_Notification_Recipient_ReadAt");
    }
}
