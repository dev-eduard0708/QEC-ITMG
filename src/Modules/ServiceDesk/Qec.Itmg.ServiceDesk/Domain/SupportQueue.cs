namespace Qec.Itmg.ServiceDesk.Domain;

public sealed class SupportQueue
{
    private SupportQueue()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static SupportQueue Create(
        string name,
        DateTimeOffset utcNow,
        string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new SupportQueue
        {
            Id = Guid.CreateVersion7(),
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            IsActive = true,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }
}

public sealed class SlaPolicy
{
    private SlaPolicy()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public TicketType? TicketType { get; private set; }

    public TicketPriority Priority { get; private set; }

    public int ResponseMinutes { get; private set; }

    public int ResolutionMinutes { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static SlaPolicy Create(
        string name,
        TicketPriority priority,
        int responseMinutes,
        int resolutionMinutes,
        DateTimeOffset utcNow,
        TicketType? ticketType = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!Enum.IsDefined(priority))
        {
            throw new ArgumentOutOfRangeException(nameof(priority));
        }

        if (ticketType is TicketType typed && !Enum.IsDefined(typed))
        {
            throw new ArgumentOutOfRangeException(nameof(ticketType));
        }

        if (responseMinutes <= 0 || resolutionMinutes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(responseMinutes), "SLA minutes must be positive.");
        }

        if (resolutionMinutes < responseMinutes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(resolutionMinutes),
                "Resolution minutes must be greater than or equal to response minutes.");
        }

        return new SlaPolicy
        {
            Id = Guid.CreateVersion7(),
            Name = name.Trim(),
            TicketType = ticketType,
            Priority = priority,
            ResponseMinutes = responseMinutes,
            ResolutionMinutes = resolutionMinutes,
            IsActive = true,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }
}

public sealed class TicketAssignmentHistory
{
    private TicketAssignmentHistory()
    {
    }

    public Guid Id { get; private set; }

    public Guid TicketId { get; private set; }

    public Guid? QueueId { get; private set; }

    public Guid? AssignedUserId { get; private set; }

    public Guid AssignedByUserId { get; private set; }

    public DateTimeOffset AssignedAtUtc { get; private set; }

    public string? Notes { get; private set; }

    public static TicketAssignmentHistory Create(
        Guid ticketId,
        Guid assignedByUserId,
        DateTimeOffset utcNow,
        Guid? queueId,
        Guid? assignedUserId,
        string? notes = null)
    {
        if (ticketId == Guid.Empty)
        {
            throw new ArgumentException("Ticket is required.", nameof(ticketId));
        }

        if (assignedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Assigned-by user is required.", nameof(assignedByUserId));
        }

        return new TicketAssignmentHistory
        {
            Id = Guid.CreateVersion7(),
            TicketId = ticketId,
            QueueId = queueId is null || queueId == Guid.Empty ? null : queueId,
            AssignedUserId = assignedUserId is null || assignedUserId == Guid.Empty ? null : assignedUserId,
            AssignedByUserId = assignedByUserId,
            AssignedAtUtc = utcNow,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
        };
    }
}
