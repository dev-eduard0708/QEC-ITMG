using Microsoft.Extensions.Options;
using Qec.Itmg.Cmdb.Services;
using Qec.Itmg.Contracts.RemoteSupport;
using Qec.Itmg.RemoteSupport;
using Qec.Itmg.RemoteSupport.Services;

namespace Qec.Itmg.Host.RemoteSupport;

public sealed record RemoteDeviceReadinessDto(
    Guid AssetId,
    string AssetNumber,
    string AssetName,
    Guid? ConfigurationItemId,
    string? ConfigurationItemNumber,
    string ReadinessStatus,
    bool RemoteReady,
    bool HasEngineMapping,
    string? Provider);

public sealed record EmployeeRemoteOnboardingDto(
    bool EngineConfigured,
    bool EngineEnabled,
    string EngineStatus,
    bool AgentDownloadConfigured,
    string? AgentDownloadUrl,
    string? AgentInstallInstructions,
    IReadOnlyList<RemoteDeviceReadinessDto> Devices,
    string OverallStatus);

public sealed class EmployeeRemoteOnboardingService(
    AssetService assets,
    IRemoteCiLookup ciLookup,
    RemoteSessionService sessions,
    IOptions<RemoteSupportOptions> options)
{
    public async Task<EmployeeRemoteOnboardingDto> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        RemoteEngineStatus engine = sessions.GetEngineStatus();
        RemoteSupportOptions cfg = options.Value;
        string? downloadUrl = cfg.HasAgentDownload ? cfg.AgentDownloadUrl.Trim() : null;
        string? instructions = string.IsNullOrWhiteSpace(cfg.AgentInstallInstructions)
            ? null
            : cfg.AgentInstallInstructions.Trim();
        IReadOnlyList<AssetDto> equipment = await assets.ListActiveEquipmentForUserAsync(userId, cancellationToken);

        List<RemoteDeviceReadinessDto> devices = [];
        foreach (AssetDto asset in equipment)
        {
            if (asset.ConfigurationItemId is not Guid ciId)
            {
                devices.Add(new RemoteDeviceReadinessDto(
                    asset.Id, asset.AssetNumber, asset.Name, null, asset.ConfigurationItemNumber,
                    "DeviceNotLinked", false, false, null));
                continue;
            }

            RemoteCiProjection? ci = await ciLookup.GetAsync(ciId, cancellationToken);
            bool mapped = !string.IsNullOrWhiteSpace(ci?.RemoteEngineNodeId);
            string status;
            bool ready;
            if (!engine.Enabled || !engine.Configured)
            {
                status = mapped ? "WaitingForIt" : "SetupRequired";
                ready = false;
            }
            else if (!mapped)
            {
                status = "SetupRequired";
                ready = false;
            }
            else
            {
                status = "Ready";
                ready = true;
            }

            devices.Add(new RemoteDeviceReadinessDto(
                asset.Id, asset.AssetNumber, asset.Name, ciId, asset.ConfigurationItemNumber ?? ci?.CiNumber,
                status, ready, mapped, mapped ? (ci?.RemoteEngineProvider ?? "MeshCentral") : null));
        }

        string overall = ResolveOverall(engine, downloadUrl, devices);
        return new EmployeeRemoteOnboardingDto(
            engine.Configured,
            engine.Enabled,
            engine.Status,
            downloadUrl is not null,
            downloadUrl,
            instructions,
            devices,
            overall);
    }

    private static string ResolveOverall(
        RemoteEngineStatus engine,
        string? downloadUrl,
        IReadOnlyList<RemoteDeviceReadinessDto> devices)
    {
        if (devices.Count == 0) return "NoDevices";
        if (devices.Any(d => d.RemoteReady)) return "Ready";
        if (!engine.Enabled || !engine.Configured) return "EngineUnavailable";
        if (downloadUrl is null && devices.Any(d => d.ReadinessStatus is "SetupRequired" or "DeviceNotLinked"))
            return "AgentNotConfigured";
        if (devices.Any(d => d.HasEngineMapping)) return "WaitingForIt";
        return "SetupRequired";
    }
}
