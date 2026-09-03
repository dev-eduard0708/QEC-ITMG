namespace Qec.Itmg.ServiceDesk.Domain;

public enum TicketType
{
    Incident = 0,
    ServiceRequest = 1,
}

public enum TicketStatus
{
    New = 0,
    Open = 1,
    InProgress = 2,
    PendingRequester = 3,
    Resolved = 4,
    Closed = 5,
    Cancelled = 6,
}

public enum TicketPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3,
}

public sealed class Ticket
{
    private Ticket()
    {
    }

    public Guid Id { get; private set; }

    public string TicketNumber { get; private set; } = null!;

    public TicketType Type { get; private set; }

    public string Title { get; private set; } = null!;

    public string Description { get; private set; } = null!;

    public TicketStatus Status { get; private set; }

    public TicketPriority Priority { get; private set; }

    public Guid RequesterUserId { get; private set; }

    public Guid? AssignedUserId { get; private set; }

    public Guid? QueueId { get; private set; }

    public Guid? ConfigurationItemId { get; private set; }

    public string? Category { get; private set; }

    public Guid? SlaPolicyId { get; private set; }

    public DateTimeOffset? ResponseDueAtUtc { get; private set; }

    public DateTimeOffset? ResolutionDueAtUtc { get; private set; }

    public DateTimeOffset? RespondedAtUtc { get; private set; }

    public bool ResponseBreached { get; private set; }

    public bool ResolutionBreached { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset? ResolvedAtUtc { get; private set; }

    public DateTimeOffset? ClosedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static Ticket Create(
        string ticketNumber,
        TicketType type,
        string title,
        string description,
        Guid requesterUserId,
        TicketPriority priority,
        DateTimeOffset utcNow,
        Guid? configurationItemId = null,
        string? category = null,
        Guid? queueId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ticketNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        if (!Enum.IsDefined(priority))
        {
            throw new ArgumentOutOfRangeException(nameof(priority));
        }

        if (requesterUserId == Guid.Empty)
        {
            throw new ArgumentException("Requester is required.", nameof(requesterUserId));
        }

        return new Ticket
        {
            Id = Guid.CreateVersion7(),
            TicketNumber = ticketNumber.Trim(),
            Type = type,
            Title = title.Trim(),
            Description = description.Trim(),
            Status = TicketStatus.New,
            Priority = priority,
            RequesterUserId = requesterUserId,
            ConfigurationItemId = NormalizeGuid(configurationItemId),
            Category = NormalizeOptional(category),
            QueueId = NormalizeGuid(queueId),
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void ApplySla(
        Guid policyId,
        DateTimeOffset responseDueAtUtc,
        DateTimeOffset resolutionDueAtUtc,
        DateTimeOffset utcNow)
    {
        if (policyId == Guid.Empty)
        {
            throw new ArgumentException("SLA policy is required.", nameof(policyId));
        }

        SlaPolicyId = policyId;
        ResponseDueAtUtc = responseDueAtUtc;
        ResolutionDueAtUtc = resolutionDueAtUtc;
        UpdatedAtUtc = utcNow;
    }

    public void UpdateDetails(
        string title,
        string description,
        TicketPriority priority,
        Guid? configurationItemId,
        string? category,
        string rowVersion,
        DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        if (!Enum.IsDefined(priority))
        {
            throw new ArgumentOutOfRangeException(nameof(priority));
        }

        EnsureRowVersion(rowVersion);

        Title = title.Trim();
        Description = description.Trim();
        Priority = priority;
        ConfigurationItemId = NormalizeGuid(configurationItemId);
        Category = NormalizeOptional(category);
        UpdatedAtUtc = utcNow;
    }

    public void ChangeStatus(TicketStatus target, DateTimeOffset utcNow, string? rowVersion = null)
    {
        if (!Enum.IsDefined(target))
        {
            throw new ArgumentOutOfRangeException(nameof(target));
        }

        if (rowVersion is not null)
        {
            EnsureRowVersion(rowVersion);
        }

        if (!IsTransitionAllowed(Status, target))
        {
            throw new InvalidOperationException($"Cannot transition ticket from {Status} to {target}.");
        }

        Status = target;
        UpdatedAtUtc = utcNow;

        if (target is TicketStatus.InProgress or TicketStatus.Open or TicketStatus.PendingRequester)
        {
            MarkResponded(utcNow);
        }

        if (target == TicketStatus.Resolved)
        {
            ResolvedAtUtc ??= utcNow;
            ClosedAtUtc = null;
        }
        else if (target == TicketStatus.Closed)
        {
            ResolvedAtUtc ??= utcNow;
            ClosedAtUtc = utcNow;
        }
        else if (target == TicketStatus.Cancelled)
        {
            ClosedAtUtc = utcNow;
        }
        else if (target is TicketStatus.New or TicketStatus.Open or TicketStatus.InProgress or TicketStatus.PendingRequester)
        {
            ClosedAtUtc = null;
            if (target != TicketStatus.Resolved)
            {
                // reopen clears resolved only when leaving resolved/closed path into active work
                if (ResolvedAtUtc is not null && target is TicketStatus.InProgress or TicketStatus.Open)
                {
                    ResolvedAtUtc = null;
                }
            }
        }
    }

    public void Assign(Guid? queueId, Guid? assignedUserId, DateTimeOffset utcNow)
    {
        if (Status is TicketStatus.Closed or TicketStatus.Cancelled)
        {
            throw new InvalidOperationException("Cannot assign a closed or cancelled ticket.");
        }

        QueueId = NormalizeGuid(queueId);
        AssignedUserId = NormalizeGuid(assignedUserId);
        UpdatedAtUtc = utcNow;

        if (AssignedUserId is not null)
        {
            MarkResponded(utcNow);
            if (Status == TicketStatus.New)
            {
                Status = TicketStatus.Open;
            }
        }
    }

    public void MarkResponseBreached(DateTimeOffset utcNow)
    {
        if (ResponseBreached || RespondedAtUtc is not null || ResponseDueAtUtc is null)
        {
            return;
        }

        if (utcNow <= ResponseDueAtUtc)
        {
            return;
        }

        ResponseBreached = true;
        UpdatedAtUtc = utcNow;
    }

    public void MarkResolutionBreached(DateTimeOffset utcNow)
    {
        if (ResolutionBreached || ResolutionDueAtUtc is null)
        {
            return;
        }

        if (Status is TicketStatus.Resolved or TicketStatus.Closed or TicketStatus.Cancelled)
        {
            return;
        }

        if (utcNow <= ResolutionDueAtUtc)
        {
            return;
        }

        ResolutionBreached = true;
        UpdatedAtUtc = utcNow;
    }

    public static bool IsTransitionAllowed(TicketStatus from, TicketStatus to)
    {
        if (from == to)
        {
            return true;
        }

        return from switch
        {
            TicketStatus.New => to is TicketStatus.Open or TicketStatus.InProgress
                or TicketStatus.PendingRequester or TicketStatus.Cancelled,
            TicketStatus.Open => to is TicketStatus.InProgress or TicketStatus.PendingRequester
                or TicketStatus.Resolved or TicketStatus.Cancelled,
            TicketStatus.InProgress => to is TicketStatus.Open or TicketStatus.PendingRequester
                or TicketStatus.Resolved or TicketStatus.Cancelled,
            TicketStatus.PendingRequester => to is TicketStatus.Open or TicketStatus.InProgress
                or TicketStatus.Resolved or TicketStatus.Cancelled,
            TicketStatus.Resolved => to is TicketStatus.Closed or TicketStatus.InProgress or TicketStatus.Open,
            TicketStatus.Closed => to is TicketStatus.InProgress,
            TicketStatus.Cancelled => false,
            _ => false,
        };
    }

    private void MarkResponded(DateTimeOffset utcNow)
    {
        RespondedAtUtc ??= utcNow;
    }

    private void EnsureRowVersion(string expectedBase64)
    {
        if (!MatchesRowVersion(RowVersion, expectedBase64))
        {
            throw new InvalidOperationException("The ticket was modified by another user.");
        }
    }

    private static bool MatchesRowVersion(byte[] current, string expectedBase64)
    {
        if (string.IsNullOrWhiteSpace(expectedBase64))
        {
            return current.Length == 0;
        }

        try
        {
            byte[] expected = Convert.FromBase64String(expectedBase64.Trim());
            return current.AsSpan().SequenceEqual(expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static Guid? NormalizeGuid(Guid? value) =>
        value is null || value == Guid.Empty ? null : value;

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
