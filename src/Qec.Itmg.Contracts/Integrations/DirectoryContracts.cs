namespace Qec.Itmg.Contracts.Integrations;

public sealed record DirectoryUserSnapshot(
    string DirectoryObjectId,
    string Upn,
    string DisplayName,
    bool Enabled,
    string? Department,
    string? JobTitle,
    IReadOnlyList<string> GroupIds,
    DateTimeOffset? LastDirectoryChangeUtc);

public enum DirectoryJmlActionKind
{
    EnableUser = 1,
    DisableUser = 2,
    AddGroupMembership = 3,
    RemoveGroupMembership = 4,
    SyncMetadata = 5,
}

public sealed record DirectoryJmlActionRequest(
    Guid AccessCaseId,
    string AccessCaseNumber,
    Guid TargetUserId,
    string TargetDirectoryObjectId,
    DirectoryJmlActionKind Action,
    string? EntitlementKey,
    string? ExternalGroupId,
    string CorrelationId);

public sealed record DirectoryJmlActionResult(
    bool Succeeded,
    bool Skipped,
    string Provider,
    string? ExternalReference,
    string Message);

/// <summary>
/// Provider-based directory read/sync and JML action execution.
/// AccessCase approvals remain authoritative — never bypass P9 SoD/checklists.
/// </summary>
public interface IDirectorySyncClient
{
    IntegrationReadiness GetReadiness();

    Task<IReadOnlyList<DirectoryUserSnapshot>> ListUsersAsync(
        CancellationToken cancellationToken = default);

    Task<DirectoryUserSnapshot?> GetUserAsync(
        string directoryObjectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a supported JML action against the directory.
    /// Must be idempotent. Must not invent success when the provider is disabled/not configured.
    /// </summary>
    Task<DirectoryJmlActionResult> ExecuteJmlActionAsync(
        DirectoryJmlActionRequest request,
        CancellationToken cancellationToken = default);
}
