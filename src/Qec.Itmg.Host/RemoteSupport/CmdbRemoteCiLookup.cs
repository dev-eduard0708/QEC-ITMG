using Qec.Itmg.Cmdb.Services;
using Qec.Itmg.Contracts.RemoteSupport;

namespace Qec.Itmg.Host.RemoteSupport;

public sealed class CmdbRemoteCiLookup(ConfigurationItemService cis) : IRemoteCiLookup
{
    public async Task<RemoteCiProjection?> GetAsync(Guid configurationItemId, CancellationToken cancellationToken = default)
    {
        ConfigurationItemDto? ci = await cis.GetConfigurationItemAsync(configurationItemId, cancellationToken);
        if (ci is null)
            return null;

        return new RemoteCiProjection(
            ci.Id,
            ci.CiNumber,
            ci.Name,
            ci.CiTypeKey,
            ci.Status,
            ci.Criticality,
            ci.RemoteEngineNodeId,
            ci.RemoteEngineProvider,
            ci.UnattendedRemotePermitted);
    }
}
