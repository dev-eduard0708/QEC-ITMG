namespace Qec.Itmg.Contracts.RemoteSupport;

/// <summary>Minimal CI projection for remote-support policy checks (implemented by Host/CMDB).</summary>
public sealed record RemoteCiProjection(
    Guid Id,
    string CiNumber,
    string Name,
    string CiTypeKey,
    string Status,
    string? Criticality,
    string? RemoteEngineNodeId,
    string? RemoteEngineProvider,
    bool UnattendedRemotePermitted);

public interface IRemoteCiLookup
{
    Task<RemoteCiProjection?> GetAsync(Guid configurationItemId, CancellationToken cancellationToken = default);
}
