using Qec.Itmg.Platform.Domain;

namespace Qec.Itmg.Platform.Notifications;

public sealed record NotificationDto(
    Guid Id,
    string Type,
    string Severity,
    string Title,
    string Message,
    string? ResourceType,
    Guid? ResourceId,
    string? ActionUrl,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ReadAtUtc,
    bool IsRead)
{
    public static NotificationDto From(Notification notification) =>
        new(
            notification.Id,
            notification.Type,
            notification.Severity.ToString(),
            notification.Title,
            notification.Message,
            notification.ResourceType,
            notification.ResourceId,
            notification.ActionUrl,
            notification.CreatedAtUtc,
            notification.ReadAtUtc,
            notification.IsRead);
}

public interface INotificationService
{
    Task<NotificationDto> CreateAsync(
        Guid recipientUserId,
        string type,
        NotificationSeverity severity,
        string title,
        string message,
        string? resourceType = null,
        Guid? resourceId = null,
        string? actionUrl = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationDto>> ListForUserAsync(
        Guid recipientUserId,
        int take = 20,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(
        Guid recipientUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a notification as read only when it belongs to <paramref name="recipientUserId"/>.
    /// Returns null when not found for that user.
    /// </summary>
    Task<NotificationDto?> MarkReadAsync(
        Guid recipientUserId,
        Guid notificationId,
        CancellationToken cancellationToken = default);
}
