namespace Qec.Itmg.Contracts.Notifications;

/// <summary>
/// Cross-module in-app notification publisher. AccessManagement and other modules depend on this
/// abstraction; Platform/Host provide the implementation (must not reverse that dependency).
/// </summary>
public interface IUserNotificationPublisher
{
    Task PublishAsync(
        Guid recipientUserId,
        string type,
        string severity, // Information|Warning|Critical as string matching platform enum names
        string title,
        string message,
        string? resourceType,
        Guid? resourceId,
        string? actionUrl,
        CancellationToken ct = default);

    /// <summary>Mark matching unread notifications as read (dedupe cleanup).</summary>
    Task MarkResourceNotificationsReadAsync(
        Guid recipientUserId,
        string type,
        string resourceType,
        Guid resourceId,
        CancellationToken ct = default);
}
