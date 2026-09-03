namespace Qec.Itmg.Platform.Domain;

public enum NotificationSeverity
{
    Info = 0,
    Warning = 1,
    Critical = 2,
}

/// <summary>
/// In-app notification for a single recipient.
/// </summary>
public sealed class Notification
{
    public Guid Id { get; private set; }

    public Guid RecipientUserId { get; private set; }

    public string Type { get; private set; } = string.Empty;

    public NotificationSeverity Severity { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

    public string? ResourceType { get; private set; }

    public Guid? ResourceId { get; private set; }

    public string? ActionUrl { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ReadAtUtc { get; private set; }

    private Notification()
    {
    }

    public static Notification Create(
        Guid recipientUserId,
        string type,
        NotificationSeverity severity,
        string title,
        string message,
        DateTimeOffset createdAtUtc,
        string? resourceType = null,
        Guid? resourceId = null,
        string? actionUrl = null)
    {
        if (recipientUserId == Guid.Empty)
        {
            throw new ArgumentException("RecipientUserId must not be empty.", nameof(recipientUserId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (!Enum.IsDefined(severity))
        {
            throw new ArgumentOutOfRangeException(nameof(severity));
        }

        return new Notification
        {
            Id = Guid.CreateVersion7(),
            RecipientUserId = recipientUserId,
            Type = type.Trim(),
            Severity = severity,
            Title = title.Trim(),
            Message = message.Trim(),
            ResourceType = string.IsNullOrWhiteSpace(resourceType) ? null : resourceType.Trim(),
            ResourceId = resourceId == Guid.Empty ? null : resourceId,
            ActionUrl = string.IsNullOrWhiteSpace(actionUrl) ? null : actionUrl.Trim(),
            CreatedAtUtc = createdAtUtc,
        };
    }

    public void MarkRead(DateTimeOffset readAtUtc)
    {
        ReadAtUtc ??= readAtUtc;
    }

    public bool IsRead => ReadAtUtc is not null;
}
