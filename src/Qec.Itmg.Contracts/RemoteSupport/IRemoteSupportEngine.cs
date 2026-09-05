namespace Qec.Itmg.Contracts.RemoteSupport;

public enum RemoteEngineHealthStatus
{
    Disabled = 0,
    NotConfigured = 1,
    Configured = 2,
    Healthy = 3,
    Unhealthy = 4,
}

public sealed record RemoteEngineStatus(
    bool Enabled,
    bool Configured,
    string ProviderKind,
    string Status,
    DateTimeOffset? LastSuccessUtc,
    DateTimeOffset? LastFailureUtc,
    string? LastErrorSummary,
    bool UnattendedEnabled);

public sealed record CreateRemoteEngineSessionRequest(
    Guid RequestId,
    string RemoteNumber,
    string EngineNodeId,
    string SessionType,
    Guid TechnicianUserId,
    Guid? TargetUserId,
    string Reason,
    string? RequestedPrivileges,
    bool Unattended);

public sealed record RemoteEngineSessionResult(
    bool Success,
    string? EngineSessionId,
    string? JoinUrl,
    string? ErrorSummary);

public sealed record RemoteEngineSessionInfo(
    string EngineSessionId,
    string Status,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    string? Outcome,
    string? EndReason,
    bool? ElevationUsed,
    string? RecordingReference);

/// <summary>
/// Provider-neutral remote support engine. ITMG owns authz/consent/audit; engine owns transport.
/// </summary>
public interface IRemoteSupportEngine
{
    RemoteEngineStatus GetStatus();

    Task<RemoteEngineSessionResult> CreateAttendedSessionAsync(
        CreateRemoteEngineSessionRequest request,
        CancellationToken cancellationToken = default);

    Task<RemoteEngineSessionResult> CreateUnattendedSessionAsync(
        CreateRemoteEngineSessionRequest request,
        CancellationToken cancellationToken = default);

    Task<RemoteEngineSessionInfo?> GetSessionAsync(
        string engineSessionId,
        CancellationToken cancellationToken = default);

    Task<RemoteEngineSessionResult> EndSessionAsync(
        string engineSessionId,
        string? reason,
        CancellationToken cancellationToken = default);
}
