using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Platform.Domain;
using Qec.Itmg.Platform.Persistence;

namespace Qec.Itmg.Platform.Notifications;

public sealed class NotificationService(
    PlatformDbContext db,
    IClock clock) : INotificationService
{
    public async Task<NotificationDto> CreateAsync(
        Guid recipientUserId,
        string type,
        NotificationSeverity severity,
        string title,
        string message,
        string? resourceType = null,
        Guid? resourceId = null,
        string? actionUrl = null,
        CancellationToken cancellationToken = default)
    {
        Notification notification = Notification.Create(
            recipientUserId,
            type,
            severity,
            title,
            message,
            clock.UtcNow,
            resourceType,
            resourceId,
            actionUrl);

        db.Notifications.Add(notification);
        await db.SaveChangesAsync(cancellationToken);
        return NotificationDto.From(notification);
    }

    public async Task<IReadOnlyList<NotificationDto>> ListForUserAsync(
        Guid recipientUserId,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        EnsureRecipient(recipientUserId);
        if (take < 1)
        {
            take = 1;
        }

        if (take > 100)
        {
            take = 100;
        }

        List<Notification> items = await db.Notifications
            .AsNoTracking()
            .Where(notification => notification.RecipientUserId == recipientUserId)
            .OrderByDescending(notification => notification.CreatedAtUtc)
            .ThenByDescending(notification => notification.Id)
            .Take(take)
            .ToListAsync(cancellationToken);

        return items.Select(NotificationDto.From).ToList();
    }

    public async Task<int> GetUnreadCountAsync(
        Guid recipientUserId,
        CancellationToken cancellationToken = default)
    {
        EnsureRecipient(recipientUserId);
        return await db.Notifications
            .AsNoTracking()
            .CountAsync(
                notification => notification.RecipientUserId == recipientUserId
                    && notification.ReadAtUtc == null,
                cancellationToken);
    }

    public async Task<NotificationDto?> MarkReadAsync(
        Guid recipientUserId,
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        EnsureRecipient(recipientUserId);

        Notification? notification = await db.Notifications.SingleOrDefaultAsync(
            candidate => candidate.Id == notificationId && candidate.RecipientUserId == recipientUserId,
            cancellationToken);

        if (notification is null)
        {
            return null;
        }

        notification.MarkRead(clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return NotificationDto.From(notification);
    }

    private static void EnsureRecipient(Guid recipientUserId)
    {
        if (recipientUserId == Guid.Empty)
        {
            throw new ArgumentException("RecipientUserId must not be empty.", nameof(recipientUserId));
        }
    }
}
