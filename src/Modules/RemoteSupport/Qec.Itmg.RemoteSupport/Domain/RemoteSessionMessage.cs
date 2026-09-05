namespace Qec.Itmg.RemoteSupport.Domain;

public enum RemoteSessionMessageType
{
    User = 0,
    System = 1,
}

public sealed class RemoteSessionMessage
{
    private RemoteSessionMessage()
    {
    }

    public Guid Id { get; private set; }
    public Guid RemoteSessionRequestId { get; private set; }
    public Guid? SenderUserId { get; private set; }
    public string MessageText { get; private set; } = null!;
    public RemoteSessionMessageType MessageType { get; private set; }
    public string? SystemEventKey { get; private set; }
    public DateTimeOffset SentAtUtc { get; private set; }

    public static RemoteSessionMessage CreateUser(
        Guid remoteSessionRequestId,
        Guid senderUserId,
        string messageText,
        DateTimeOffset utcNow)
    {
        if (remoteSessionRequestId == Guid.Empty)
            throw new ArgumentException("Session is required.", nameof(remoteSessionRequestId));
        if (senderUserId == Guid.Empty)
            throw new ArgumentException("Sender is required.", nameof(senderUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(messageText);
        string text = messageText.Trim();
        if (text.Length > 4000)
            throw new ArgumentException("Message is too long.", nameof(messageText));

        return new RemoteSessionMessage
        {
            Id = Guid.CreateVersion7(),
            RemoteSessionRequestId = remoteSessionRequestId,
            SenderUserId = senderUserId,
            MessageText = text,
            MessageType = RemoteSessionMessageType.User,
            SentAtUtc = utcNow,
        };
    }

    public static RemoteSessionMessage CreateSystem(
        Guid remoteSessionRequestId,
        string systemEventKey,
        string messageText,
        DateTimeOffset utcNow)
    {
        if (remoteSessionRequestId == Guid.Empty)
            throw new ArgumentException("Session is required.", nameof(remoteSessionRequestId));
        ArgumentException.ThrowIfNullOrWhiteSpace(systemEventKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageText);

        return new RemoteSessionMessage
        {
            Id = Guid.CreateVersion7(),
            RemoteSessionRequestId = remoteSessionRequestId,
            SenderUserId = null,
            MessageText = messageText.Trim(),
            MessageType = RemoteSessionMessageType.System,
            SystemEventKey = systemEventKey.Trim(),
            SentAtUtc = utcNow,
        };
    }
}
