namespace Qec.Itmg.Contracts.Integrations;

/// <summary>
/// Snapshot of a Veeam backup job run. Read-only; no write/remote-control operations.
/// </summary>
public sealed record VeeamJobRunSnapshot(
    string JobId,
    string JobName,
    string Status,
    DateTimeOffset? StartTime,
    DateTimeOffset? EndTime,
    long ProcessedObjects,
    long TransferredBytes);

/// <summary>
/// Snapshot of a protected workload as Veeam knows it.
/// </summary>
public sealed record VeeamProtectedWorkload(
    string ObjectId,
    string Name,
    string Platform,
    string? LastBackupStatus,
    DateTimeOffset? LastBackupTime,
    string? RepositoryName);

/// <summary>
/// Snapshot of a Veeam backup repository.
/// </summary>
public sealed record VeeamRepositorySnapshot(
    string RepositoryId,
    string Name,
    long CapacityBytes,
    long FreeBytes);

/// <summary>
/// Restore point information (read-only; no restore commands).
/// </summary>
public sealed record VeeamRestorePoint(
    string RestorePointId,
    string JobName,
    string ObjectName,
    DateTimeOffset CreationTime,
    string BackupType);

/// <summary>
/// Read-only future interface for Veeam Backup &amp; Replication / Enterprise Manager.
/// Production connections require explicit QEC authorization (P19+).
/// </summary>
public interface IVeeamClient
{
    /// <summary>Returns readiness state without contacting the vendor system.</summary>
    IntegrationReadiness GetReadiness();

    Task<IReadOnlyList<VeeamJobRunSnapshot>> GetRecentJobRunsAsync(
        int maxResults = 50,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VeeamProtectedWorkload>> GetProtectedWorkloadsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VeeamRepositorySnapshot>> GetRepositoriesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VeeamRestorePoint>> GetRestorePointsAsync(
        string? objectName = null,
        CancellationToken cancellationToken = default);
}
