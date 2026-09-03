namespace Qec.Itmg.Contracts.Integrations;

/// <summary>
/// Snapshot of an endpoint device as SonicWall Capture Client knows it.
/// </summary>
public sealed record SonicWallEndpointSnapshot(
    string DeviceId,
    string DeviceName,
    string? Platform,
    string ProtectionStatus,
    DateTimeOffset? LastSeenUtc,
    bool AgentInstalled,
    int ThreatCount,
    int QuarantinedCount);

/// <summary>
/// Snapshot of a detection/threat event. Read-only.
/// </summary>
public sealed record SonicWallDetectionSnapshot(
    string DetectionId,
    string DeviceId,
    string ThreatName,
    string Severity,
    DateTimeOffset DetectedAtUtc,
    string Status);

/// <summary>
/// Read-only future interface for SonicWall Capture Client.
/// Production connections require explicit QEC authorization (P19+).
/// This is NOT the malware scanner for P2-03 — that uses IMalwareScanner.
/// </summary>
public interface ISonicWallCaptureClient
{
    /// <summary>Returns readiness state without contacting the vendor system.</summary>
    IntegrationReadiness GetReadiness();

    Task<IReadOnlyList<SonicWallEndpointSnapshot>> GetEndpointsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SonicWallDetectionSnapshot>> GetRecentDetectionsAsync(
        int maxResults = 100,
        CancellationToken cancellationToken = default);
}
