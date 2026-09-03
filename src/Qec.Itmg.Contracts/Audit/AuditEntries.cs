namespace Qec.Itmg.Contracts.Audit;

public sealed class BusinessAuditEntry
{
    public required AuditAggregateType AggregateType { get; init; }

    public required Guid AggregateId { get; init; }

    public string? BusinessNumber { get; init; }

    public required BusinessAuditAction Action { get; init; }

    public string? FieldName { get; init; }

    public string? OldValue { get; init; }

    public string? NewValue { get; init; }

    public string? Reason { get; init; }

    public AuditSource Source { get; init; } = AuditSource.Api;
}

public sealed class SecurityAuditEntry
{
    public required SecurityEventType EventType { get; init; }

    public required SecurityEventOutcome Outcome { get; init; }

    public string? TargetType { get; init; }

    public string? TargetId { get; init; }

    public string? Details { get; init; }
}
