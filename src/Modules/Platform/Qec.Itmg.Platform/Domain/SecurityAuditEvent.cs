using Qec.Itmg.Contracts.Audit;

namespace Qec.Itmg.Platform.Domain;

public sealed class SecurityAuditEvent
{
    private SecurityAuditEvent()
    {
    }

    public Guid Id { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public SecurityEventType EventType { get; private set; }

    public SecurityEventOutcome Outcome { get; private set; }

    public Guid? ActorUserId { get; private set; }

    public string? TargetType { get; private set; }

    public string? TargetId { get; private set; }

    public string? Details { get; private set; }

    public string? CorrelationId { get; private set; }

    public string? ClientIp { get; private set; }

    public static SecurityAuditEvent Create(
        SecurityAuditEntry entry,
        DateTimeOffset occurredAtUtc,
        Guid? actorUserId,
        string? correlationId,
        string? clientIp)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new SecurityAuditEvent
        {
            Id = Guid.CreateVersion7(),
            OccurredAtUtc = occurredAtUtc,
            EventType = entry.EventType,
            Outcome = entry.Outcome,
            ActorUserId = actorUserId,
            TargetType = NormalizeOptional(entry.TargetType),
            TargetId = NormalizeOptional(entry.TargetId),
            Details = entry.Details,
            CorrelationId = NormalizeOptional(correlationId),
            ClientIp = NormalizeOptional(clientIp),
        };
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
