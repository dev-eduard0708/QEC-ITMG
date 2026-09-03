using Qec.Itmg.Contracts.Audit;

namespace Qec.Itmg.Platform.Domain;

public sealed class BusinessAuditRecord
{
    private BusinessAuditRecord()
    {
    }

    public Guid Id { get; private set; }

    public AuditAggregateType AggregateType { get; private set; }

    public Guid AggregateId { get; private set; }

    public string? BusinessNumber { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public Guid? ActorUserId { get; private set; }

    public AuditActorType ActorType { get; private set; }

    public string? JobName { get; private set; }

    public AuditSource Source { get; private set; }

    public BusinessAuditAction Action { get; private set; }

    public string? FieldName { get; private set; }

    public string? OldValue { get; private set; }

    public string? NewValue { get; private set; }

    public string? Reason { get; private set; }

    public string? CorrelationId { get; private set; }

    public string? ClientIp { get; private set; }

    public static BusinessAuditRecord Create(
        BusinessAuditEntry entry,
        DateTimeOffset occurredAtUtc,
        Guid? actorUserId,
        AuditActorType actorType,
        string? jobName,
        string? correlationId,
        string? clientIp)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new BusinessAuditRecord
        {
            Id = Guid.CreateVersion7(),
            AggregateType = entry.AggregateType,
            AggregateId = entry.AggregateId,
            BusinessNumber = NormalizeOptional(entry.BusinessNumber),
            OccurredAtUtc = occurredAtUtc,
            ActorUserId = actorUserId,
            ActorType = actorType,
            JobName = NormalizeOptional(jobName),
            Source = entry.Source,
            Action = entry.Action,
            FieldName = NormalizeOptional(entry.FieldName),
            OldValue = entry.OldValue,
            NewValue = entry.NewValue,
            Reason = NormalizeOptional(entry.Reason),
            CorrelationId = NormalizeOptional(correlationId),
            ClientIp = NormalizeOptional(clientIp),
        };
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
