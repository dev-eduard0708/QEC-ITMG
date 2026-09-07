using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Notifications;
using Qec.Itmg.Platform.Domain;
using Qec.Itmg.Platform.Persistence;

namespace Qec.Itmg.Platform.Notifications;

/// <summary>
/// Platform adapter for <see cref="IUserNotificationPublisher"/> with unread dedupe on publish.
/// </summary>
public sealed class UserNotificationPublisher(
    PlatformDbContext db,
    IClock clock) : IUserNotificationPublisher
{
    public async Task PublishAsync(
        Guid recipientUserId,
        string type,
        string severity,
        string title,
        string message,
        string? resourceType,
        Guid? resourceId,
        string? actionUrl,
        CancellationToken ct = default)
    {
        if (recipientUserId == Guid.Empty)
            throw new ArgumentException("RecipientUserId must not be empty.", nameof(recipientUserId));

        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        NotificationSeverity parsedSeverity = ParseSeverity(severity);
        string normalizedType = type.Trim();
        string? normalizedResourceType = string.IsNullOrWhiteSpace(resourceType) ? null : resourceType.Trim();
        Guid? normalizedResourceId = resourceId is null || resourceId == Guid.Empty ? null : resourceId;

        if (normalizedResourceType is not null && normalizedResourceId is Guid rid)
        {
            bool unreadExists = await db.Notifications.AsNoTracking().AnyAsync(
                n => n.RecipientUserId == recipientUserId
                    && n.Type == normalizedType
                    && n.ResourceType == normalizedResourceType
                    && n.ResourceId == rid
                    && n.ReadAtUtc == null,
                ct);
            if (unreadExists)
                return;
        }

        Notification notification = Notification.Create(
            recipientUserId,
            normalizedType,
            parsedSeverity,
            title,
            message,
            clock.UtcNow,
            normalizedResourceType,
            normalizedResourceId,
            actionUrl);

        db.Notifications.Add(notification);
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkResourceNotificationsReadAsync(
        Guid recipientUserId,
        string type,
        string resourceType,
        Guid resourceId,
        CancellationToken ct = default)
    {
        if (recipientUserId == Guid.Empty)
            throw new ArgumentException("RecipientUserId must not be empty.", nameof(recipientUserId));

        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceType);
        if (resourceId == Guid.Empty)
            throw new ArgumentException("ResourceId must not be empty.", nameof(resourceId));

        string normalizedType = type.Trim();
        string normalizedResourceType = resourceType.Trim();
        DateTimeOffset now = clock.UtcNow;

        List<Notification> unread = await db.Notifications
            .Where(n => n.RecipientUserId == recipientUserId
                && n.Type == normalizedType
                && n.ResourceType == normalizedResourceType
                && n.ResourceId == resourceId
                && n.ReadAtUtc == null)
            .ToListAsync(ct);

        if (unread.Count == 0)
            return;

        foreach (Notification notification in unread)
            notification.MarkRead(now);

        await db.SaveChangesAsync(ct);
    }

    private static NotificationSeverity ParseSeverity(string severity)
    {
        if (string.IsNullOrWhiteSpace(severity))
            throw new ArgumentException("Severity is required.", nameof(severity));

        string trimmed = severity.Trim();
        if (trimmed.Equals("Information", StringComparison.OrdinalIgnoreCase))
            return NotificationSeverity.Info;

        if (Enum.TryParse(trimmed, ignoreCase: true, out NotificationSeverity parsed)
            && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        throw new ArgumentException(
            $"Severity must be Info, Information, Warning, or Critical (got '{severity}').",
            nameof(severity));
    }
}
