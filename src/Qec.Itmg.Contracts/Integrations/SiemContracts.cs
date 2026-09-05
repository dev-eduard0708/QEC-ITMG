namespace Qec.Itmg.Contracts.Integrations;

public enum SiemEventSeverity
{
    Info = 1,
    Low = 2,
    Medium = 3,
    High = 4,
    Critical = 5,
}

/// <summary>
/// Structured outbound SIEM event. Must not contain passwords, tokens, secret references, or evidence contents.
/// </summary>
public sealed record SiemOutboundEvent(
    string EventId,
    DateTimeOffset TimestampUtc,
    string EventType,
    SiemEventSeverity Severity,
    string SourceSystem,
    string? ActorUpn,
    string? AggregateType,
    string? AggregateId,
    string? CorrelationId,
    IReadOnlyDictionary<string, string> Attributes);

public sealed record SiemPublishResult(bool Accepted, bool Queued, string? DeliveryId, string Message);

public interface ISiemPublisher
{
    IntegrationReadiness GetReadiness();

    /// <summary>
    /// Enqueues or delivers an event. Must not throw into caller business transactions when delivery fails —
    /// implementations should queue/retry and return a result.
    /// </summary>
    Task<SiemPublishResult> PublishAsync(SiemOutboundEvent evt, CancellationToken cancellationToken = default);
}
