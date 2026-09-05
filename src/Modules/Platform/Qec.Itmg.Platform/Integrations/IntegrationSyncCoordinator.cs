using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Contracts.Integrations;
using Qec.Itmg.Contracts.Security;
using Qec.Itmg.Platform.Domain;
using Qec.Itmg.Platform.Persistence;

namespace Qec.Itmg.Platform.Integrations;

public sealed record IntegrationSyncResult(
    string Provider,
    string Status,
    int Processed,
    int Succeeded,
    int Failed,
    int Unmatched,
    string? Message,
    string CorrelationId);

/// <summary>
/// Coordinates provider sync without silently enabling integrations.
/// Heavy domain upserts for ops/security/identity are handled by Host job wrappers when registered.
/// </summary>
public sealed class IntegrationSyncCoordinator(
    IOptions<IntegrationOptions> options,
    IntegrationRunService runs,
    IntegrationHealthState health,
    IVeeamClient veeam,
    ISynologyMonitor synology,
    ISonicWallCaptureClient sonicWall,
    IDirectorySyncClient directory,
    IVirtualizationEnrichmentClient virtualization,
    IVulnerabilityScannerIngestClient scanner,
    PlatformDbContext db,
    IBusinessAuditWriter audit,
    IClock clock,
    ILogger<IntegrationSyncCoordinator> logger,
    IIntegrationDomainSync? domainSync = null)
{
    public async Task<IntegrationSyncResult> SyncProviderAsync(string provider, CancellationToken ct)
    {
        string key = (provider ?? string.Empty).Trim().ToLowerInvariant();
        return key switch
        {
            "veeam" => await RunAsync("Veeam", options.Value.Veeam, IntegrationProvider.Veeam, SyncVeeamAsync, ct),
            "synology" => await RunAsync("Synology", options.Value.Synology, IntegrationProvider.Synology, SyncSynologyAsync, ct),
            "sonicwallcaptureclient" or "sonicwall" =>
                await RunAsync("SonicWallCaptureClient", options.Value.SonicWallCaptureClient, IntegrationProvider.SonicWallCaptureClient, SyncSonicWallAsync, ct),
            "directory" => await RunAsync("Directory", options.Value.Directory, IntegrationProvider.Directory, SyncDirectoryAsync, ct),
            "virtualization" or "vcenter" or "hyperv" =>
                await RunAsync("Virtualization", options.Value.Virtualization, IntegrationProvider.Virtualization, SyncVirtualizationAsync, ct),
            "vulnerabilityscanner" or "vulnscanner" =>
                await RunAsync("VulnerabilityScanner", options.Value.VulnerabilityScanner, IntegrationProvider.VulnerabilityScanner, SyncVulnAsync, ct),
            _ => new IntegrationSyncResult(key.Length == 0 ? "unknown" : key, "Failed", 0, 0, 0, 0, "Unknown provider.", Guid.NewGuid().ToString("N")),
        };
    }

    public async Task<IReadOnlyList<IntegrationSyncResult>> SyncEnabledAsync(CancellationToken ct)
    {
        List<IntegrationSyncResult> results = [];
        if (options.Value.Veeam.Enabled) results.Add(await SyncProviderAsync("veeam", ct));
        if (options.Value.Synology.Enabled) results.Add(await SyncProviderAsync("synology", ct));
        if (options.Value.SonicWallCaptureClient.Enabled) results.Add(await SyncProviderAsync("sonicwall", ct));
        if (options.Value.Directory.Enabled) results.Add(await SyncProviderAsync("directory", ct));
        if (options.Value.Virtualization.Enabled) results.Add(await SyncProviderAsync("virtualization", ct));
        if (options.Value.VulnerabilityScanner.Enabled) results.Add(await SyncProviderAsync("vulnerabilityscanner", ct));
        return results;
    }

    private async Task<IntegrationSyncResult> RunAsync(
        string providerName,
        IntegrationVendorOptions opts,
        IntegrationProvider provider,
        Func<CancellationToken, Task<(int processed, int succeeded, int failed, int unmatched, string? message)>> action,
        CancellationToken ct)
    {
        if (!opts.Enabled)
            return new(providerName, "Skipped", 0, 0, 0, 0, "Integration disabled.", Guid.NewGuid().ToString("N"));
        if (!opts.IsConfigured && opts.RequiresBaseUrl)
            return new(providerName, "Skipped", 0, 0, 0, 0, "Integration not configured.", Guid.NewGuid().ToString("N"));
        if (!opts.RequiresBaseUrl && !opts.IsConfiguredRelaxed)
            return new(providerName, "Skipped", 0, 0, 0, 0, "Integration not configured.", Guid.NewGuid().ToString("N"));

        if (await runs.HasRunningAsync(providerName, "Sync", ct))
            return new(providerName, "Skipped", 0, 0, 0, 0, "Overlapping run prevented.", Guid.NewGuid().ToString("N"));

        IntegrationRun run = await runs.StartAsync(providerName, "Sync", ct);
        try
        {
            var (processed, succeeded, failed, unmatched, message) = await action(ct);
            await runs.CompleteAsync(run, IntegrationRunStatus.Succeeded, processed, succeeded, failed, unmatched, message, ct);
            health.RecordSuccess(provider, clock.UtcNow, processed, unmatched);
            await audit.AppendAsync(new BusinessAuditEntry
            {
                AggregateType = AuditAggregateType.IntegrationRun,
                AggregateId = run.Id,
                BusinessNumber = providerName,
                Action = BusinessAuditAction.Updated,
                FieldName = "IntegrationSync",
                NewValue = $"{{\"processed\":{processed},\"unmatched\":{unmatched}}}",
                Source = AuditSource.Job,
            }, ct);
            return new(providerName, "Succeeded", processed, succeeded, failed, unmatched, message, run.CorrelationId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Integration sync failed for {Provider}", providerName);
            health.RecordFailure(provider, clock.UtcNow, "sync-failed");
            await runs.CompleteAsync(run, IntegrationRunStatus.Failed, 0, 0, 1, 0, "sync-failed", ct);
            return new(providerName, "Failed", 0, 0, 1, 0, "Sync failed.", run.CorrelationId);
        }
    }

    private async Task<(int, int, int, int, string?)> SyncVeeamAsync(CancellationToken ct)
    {
        IReadOnlyList<VeeamJobRunSnapshot> runsList = await veeam.GetRecentJobRunsAsync(50, ct);
        int upserted = domainSync is null ? runsList.Count : await domainSync.UpsertVeeamRunsAsync(runsList, ct);
        return (runsList.Count, upserted, 0, 0, null);
    }

    private async Task<(int, int, int, int, string?)> SyncSynologyAsync(CancellationToken ct)
    {
        SynologySystemSnapshot? system = await synology.GetSystemSnapshotAsync(ct);
        int n = system is null ? 0 : (domainSync is null ? 1 : await domainSync.UpsertSynologyAsync(system, ct));
        return (n, n, 0, 0, null);
    }

    private async Task<(int, int, int, int, string?)> SyncSonicWallAsync(CancellationToken ct)
    {
        IReadOnlyList<SonicWallDetectionSnapshot> detections = await sonicWall.GetRecentDetectionsAsync(100, ct);
        int n = domainSync is null ? detections.Count : await domainSync.UpsertSonicWallDetectionsAsync(detections, ct);
        return (detections.Count, n, 0, 0, null);
    }

    private async Task<(int, int, int, int, string?)> SyncDirectoryAsync(CancellationToken ct)
    {
        IReadOnlyList<DirectoryUserSnapshot> users = await directory.ListUsersAsync(ct);
        int n = domainSync is null ? users.Count : await domainSync.UpsertDirectoryUsersAsync(users, ct);
        return (users.Count, n, 0, 0, null);
    }

    private async Task<(int, int, int, int, string?)> SyncVirtualizationAsync(CancellationToken ct)
    {
        IReadOnlyList<VirtualMachineSnapshot> vms = await virtualization.ListVirtualMachinesAsync(ct);
        if (domainSync is not null)
            return await domainSync.CorrelateVirtualMachinesAsync(vms, ct);

        int unmatched = 0;
        foreach (VirtualMachineSnapshot vm in vms)
        {
            IntegrationCorrelation? existing = await db.IntegrationCorrelations
                .FirstOrDefaultAsync(x => x.Provider == vm.Provider && x.ExternalId == vm.ExternalId && x.TargetType == "ConfigurationItem", ct);
            if (existing is null)
            {
                db.IntegrationCorrelations.Add(IntegrationCorrelation.Create(
                    vm.Provider, vm.ExternalId, "ConfigurationItem", "Unmatched", clock.UtcNow, displayName: vm.Name));
                unmatched++;
            }
        }
        await db.SaveChangesAsync(ct);
        return (vms.Count, vms.Count - unmatched, 0, unmatched, null);
    }

    private async Task<(int, int, int, int, string?)> SyncVulnAsync(CancellationToken ct)
    {
        var items = await scanner.FetchAsync(ct);
        int n = domainSync is null ? items.Count : await domainSync.IngestVulnerabilitiesAsync(items, ct);
        int unmatched = await db.IntegrationCorrelations.CountAsync(
            x => x.Provider == "VulnerabilityScanner" && x.MatchStatus == "Unmatched", ct);
        return (items.Count + unmatched, n, 0, unmatched, null);
    }
}

/// <summary>Optional Host-side domain upsert hooks.</summary>
public interface IIntegrationDomainSync
{
    Task<int> UpsertVeeamRunsAsync(IReadOnlyList<VeeamJobRunSnapshot> runs, CancellationToken ct);
    Task<int> UpsertSynologyAsync(SynologySystemSnapshot system, CancellationToken ct);
    Task<int> UpsertSonicWallDetectionsAsync(IReadOnlyList<SonicWallDetectionSnapshot> detections, CancellationToken ct);
    Task<int> UpsertDirectoryUsersAsync(IReadOnlyList<DirectoryUserSnapshot> users, CancellationToken ct);
    Task<(int processed, int succeeded, int failed, int unmatched, string? message)> CorrelateVirtualMachinesAsync(
        IReadOnlyList<VirtualMachineSnapshot> vms, CancellationToken ct);
    Task<int> IngestVulnerabilitiesAsync(IReadOnlyList<Qec.Itmg.Contracts.Security.ScannerVulnerabilityIngestItem> items, CancellationToken ct);
}
