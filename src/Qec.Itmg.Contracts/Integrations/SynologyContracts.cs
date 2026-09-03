namespace Qec.Itmg.Contracts.Integrations;

/// <summary>
/// Snapshot of a Synology NAS system health and storage state.
/// </summary>
public sealed record SynologySystemSnapshot(
    string DeviceId,
    string Hostname,
    string DsmVersion,
    string SystemStatus,
    long TotalCapacityBytes,
    long UsedCapacityBytes,
    long FreeCapacityBytes);

/// <summary>
/// Snapshot of a single Synology volume.
/// </summary>
public sealed record SynologyVolumeSnapshot(
    string VolumeId,
    string VolumeName,
    string RaidType,
    string Status,
    long CapacityBytes,
    long UsedBytes);

/// <summary>
/// Snapshot of a disk health entry.
/// </summary>
public sealed record SynologyDiskSnapshot(
    string DiskId,
    string Model,
    string Status,
    string SmartStatus,
    long CapacityBytes);

/// <summary>
/// Snapshot of replication task status.
/// </summary>
public sealed record SynologyReplicationSnapshot(
    string TaskId,
    string TaskName,
    string TargetHost,
    string Status,
    DateTimeOffset? LastSyncUtc);

/// <summary>
/// Read-only future interface for Synology DSM monitoring.
/// Production connections require explicit QEC authorization (P19+).
/// </summary>
public interface ISynologyMonitor
{
    /// <summary>Returns readiness state without contacting the vendor system.</summary>
    IntegrationReadiness GetReadiness();

    Task<SynologySystemSnapshot?> GetSystemSnapshotAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SynologyVolumeSnapshot>> GetVolumesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SynologyDiskSnapshot>> GetDisksAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SynologyReplicationSnapshot>> GetReplicationTasksAsync(
        CancellationToken cancellationToken = default);
}
