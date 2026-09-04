namespace Qec.Itmg.Operations.Domain;

public enum EventSeverity
{
    Info = 0,
    Warning = 1,
    Critical = 2,
    Emergency = 3,
}

public enum EventStatus
{
    New = 0,
    Acknowledged = 1,
    Promoted = 2,
    Closed = 3,
}

public sealed class OperationalEvent
{
    private OperationalEvent()
    {
    }

    public Guid Id { get; private set; }
    public string EventNumber { get; private set; } = null!;
    public string Source { get; private set; } = null!;
    public string SourceEventKey { get; private set; } = null!;
    public EventSeverity Severity { get; private set; }
    public string Title { get; private set; } = null!;
    public string Summary { get; private set; } = null!;
    public Guid? ConfigurationItemId { get; private set; }
    public EventStatus Status { get; private set; }
    public int OccurrenceCount { get; private set; }
    public DateTimeOffset FirstSeenAtUtc { get; private set; }
    public DateTimeOffset LastSeenAtUtc { get; private set; }
    public DateTimeOffset? AcknowledgedAtUtc { get; private set; }
    public Guid? AcknowledgedByUserId { get; private set; }
    public Guid? LinkedTicketId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static OperationalEvent Create(
        string eventNumber,
        string source,
        string sourceEventKey,
        EventSeverity severity,
        string title,
        string summary,
        DateTimeOffset utcNow,
        Guid? configurationItemId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEventKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        if (!Enum.IsDefined(severity)) throw new ArgumentOutOfRangeException(nameof(severity));

        return new OperationalEvent
        {
            Id = Guid.CreateVersion7(),
            EventNumber = eventNumber.Trim(),
            Source = source.Trim(),
            SourceEventKey = sourceEventKey.Trim(),
            Severity = severity,
            Title = title.Trim(),
            Summary = summary.Trim(),
            ConfigurationItemId = NormalizeGuid(configurationItemId),
            Status = EventStatus.New,
            OccurrenceCount = 1,
            FirstSeenAtUtc = utcNow,
            LastSeenAtUtc = utcNow,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void RecordOccurrence(
        EventSeverity severity,
        string title,
        string summary,
        Guid? configurationItemId,
        DateTimeOffset utcNow)
    {
        if (Status == EventStatus.Closed)
        {
            throw new InvalidOperationException("Closed events cannot accept new occurrences.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        if (!Enum.IsDefined(severity)) throw new ArgumentOutOfRangeException(nameof(severity));

        Severity = severity;
        Title = title.Trim();
        Summary = summary.Trim();
        ConfigurationItemId = NormalizeGuid(configurationItemId) ?? ConfigurationItemId;
        OccurrenceCount += 1;
        LastSeenAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
        if (Status == EventStatus.Acknowledged)
        {
            // Re-open attention when a new occurrence arrives after ack (still not promoted).
            Status = EventStatus.New;
            AcknowledgedAtUtc = null;
            AcknowledgedByUserId = null;
        }
    }

    public void Acknowledge(Guid userId, DateTimeOffset utcNow)
    {
        if (userId == Guid.Empty) throw new ArgumentException("User is required.", nameof(userId));
        if (Status is EventStatus.Promoted or EventStatus.Closed)
        {
            throw new InvalidOperationException($"Cannot acknowledge an event in status {Status}.");
        }

        Status = EventStatus.Acknowledged;
        AcknowledgedAtUtc = utcNow;
        AcknowledgedByUserId = userId;
        UpdatedAtUtc = utcNow;
    }

    public void MarkPromoted(Guid ticketId, DateTimeOffset utcNow)
    {
        if (ticketId == Guid.Empty) throw new ArgumentException("Ticket is required.", nameof(ticketId));
        if (Status == EventStatus.Closed)
        {
            throw new InvalidOperationException("Closed events cannot be promoted.");
        }

        Status = EventStatus.Promoted;
        LinkedTicketId = ticketId;
        UpdatedAtUtc = utcNow;
    }

    public void Close(DateTimeOffset utcNow)
    {
        if (Status == EventStatus.Promoted && LinkedTicketId is null)
        {
            throw new InvalidOperationException("Promoted events must retain a linked ticket.");
        }

        Status = EventStatus.Closed;
        UpdatedAtUtc = utcNow;
    }

    private static Guid? NormalizeGuid(Guid? value) =>
        value is null || value == Guid.Empty ? null : value;
}
