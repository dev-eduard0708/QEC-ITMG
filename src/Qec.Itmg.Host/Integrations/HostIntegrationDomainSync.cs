using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Cmdb.Services;
using Qec.Itmg.Contracts.Integrations;
using Qec.Itmg.Contracts.Security;
using Qec.Itmg.Identity.Domain;
using Qec.Itmg.Identity.Persistence;
using Qec.Itmg.Operations.Domain;
using Qec.Itmg.Operations.Services;
using Qec.Itmg.Platform.Domain;
using Qec.Itmg.Platform.Integrations;
using Qec.Itmg.Platform.Persistence;
using Qec.Itmg.Security.Services;

namespace Qec.Itmg.Host.Integrations;

public sealed class HostIntegrationDomainSync(
    OpsRecordsService ops,
    EventService events,
    SecurityService security,
    ConfigurationItemService cis,
    IdentityDbContext identity,
    PlatformDbContext platform,
    IClock clock,
    ILogger<HostIntegrationDomainSync> logger) : IIntegrationDomainSync
{
    public async Task<int> UpsertVeeamRunsAsync(IReadOnlyList<VeeamJobRunSnapshot> runs, CancellationToken ct)
    {
        int count = 0;
        foreach (VeeamJobRunSnapshot run in runs)
        {
            var jobs = await ops.ListBackupJobsAsync(1, 50, run.JobName, ct);
            BackupJobDto? job = jobs.Items.FirstOrDefault(j =>
                string.Equals(j.ExternalJobId, run.JobId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(j.Name, run.JobName, StringComparison.OrdinalIgnoreCase));
            if (job is null)
            {
                job = await ops.CreateBackupJobAsync(run.JobName, "Veeam", run.JobId, null, ct);
            }

            string extRef = $"{run.JobId}:{run.StartTime?.UtcTicks ?? 0}";
            var existingRuns = await ops.ListBackupRunsAsync(1, 20, job.Id, null, ct);
            if (existingRuns.Items.Any(r => string.Equals(r.ExternalReference, extRef, StringComparison.Ordinal)))
                continue;

            await ops.CreateBackupRunAsync(
                job.Id,
                run.StartTime ?? clock.UtcNow,
                NormalizeBackupStatus(run.Status),
                summary: $"{run.JobName} · {run.Status}",
                externalReference: extRef,
                completedAtUtc: run.EndTime,
                ct);
            await events.IngestAsync(
                "Veeam",
                extRef,
                MapSeverity(run.Status),
                $"Veeam job {run.JobName}",
                $"Status={run.Status}; processed={run.ProcessedObjects}",
                configurationItemId: job.ConfigurationItemId,
                cancellationToken: ct);
            count++;
        }
        return count;
    }

    public async Task<int> UpsertSynologyAsync(SynologySystemSnapshot system, CancellationToken ct)
    {
        await events.IngestAsync(
            "Synology",
            $"system:{system.DeviceId}",
            EventSeverity.Info,
            $"Synology {system.Hostname}",
            $"Status={system.SystemStatus}; DSM={system.DsmVersion}",
            cancellationToken: ct);
        return 1;
    }

    public async Task<int> UpsertSonicWallDetectionsAsync(IReadOnlyList<SonicWallDetectionSnapshot> detections, CancellationToken ct)
    {
        int count = 0;
        foreach (SonicWallDetectionSnapshot d in detections)
        {
            await events.IngestAsync(
                "SonicWallCaptureClient",
                d.DetectionId,
                MapSeverity(d.Severity),
                d.ThreatName,
                $"Device={d.DeviceId}; Status={d.Status}",
                cancellationToken: ct);
            count++;
        }
        return count;
    }

    public async Task<int> UpsertDirectoryUsersAsync(IReadOnlyList<DirectoryUserSnapshot> users, CancellationToken ct)
    {
        int count = 0;
        foreach (DirectoryUserSnapshot snap in users)
        {
            User? byDir = await identity.Users.FirstOrDefaultAsync(u => u.DirectoryObjectId == snap.DirectoryObjectId, ct);
            User? byUpn = byDir ?? await identity.Users.FirstOrDefaultAsync(u => u.Upn == snap.Upn, ct);
            if (byUpn is null)
            {
                User created = User.Create(snap.Upn, snap.DisplayName, UserType.Employee, clock.UtcNow, snap.DirectoryObjectId);
                if (!snap.Enabled)
                    created.Disable(clock.UtcNow);
                identity.Users.Add(created);
                count++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(byUpn.DirectoryObjectId))
            {
                try { byUpn.BindDirectoryObjectId(snap.DirectoryObjectId, clock.UtcNow); }
                catch (Exception ex) { logger.LogDebug(ex, "Directory bind skipped for {Upn}", snap.Upn); }
            }

            if (!string.Equals(byUpn.DisplayName, snap.DisplayName, StringComparison.Ordinal))
                byUpn.Rename(snap.DisplayName, clock.UtcNow);

            if (snap.Enabled && byUpn.Status == UserStatus.Disabled)
                byUpn.Enable(clock.UtcNow);
            else if (!snap.Enabled && byUpn.Status == UserStatus.Active)
                byUpn.Disable(clock.UtcNow);

            count++;
        }
        await identity.SaveChangesAsync(ct);
        return count;
    }

    public async Task<(int processed, int succeeded, int failed, int unmatched, string? message)> CorrelateVirtualMachinesAsync(
        IReadOnlyList<VirtualMachineSnapshot> vms, CancellationToken ct)
    {
        var ciList = await cis.ListConfigurationItemsAsync(null, ct);
        int matched = 0, unmatched = 0;
        foreach (VirtualMachineSnapshot vm in vms)
        {
            var byName = ciList.Where(c =>
                string.Equals(c.Name, vm.Name, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(vm.BiosUuid) && string.Equals(c.SerialNumber, vm.BiosUuid, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            Guid? ciId = byName.Count == 1 ? byName[0].Id : null;
            string status = byName.Count == 1 ? "Matched" : byName.Count > 1 ? "Ambiguous" : "Unmatched";
            if (ciId is null) unmatched++; else matched++;

            IntegrationCorrelation? existing = await platform.IntegrationCorrelations
                .FirstOrDefaultAsync(x => x.Provider == vm.Provider && x.ExternalId == vm.ExternalId && x.TargetType == "ConfigurationItem", ct);
            if (existing is null)
            {
                platform.IntegrationCorrelations.Add(IntegrationCorrelation.Create(
                    vm.Provider, vm.ExternalId, "ConfigurationItem", status, clock.UtcNow, ciId, vm.Name,
                    metadataJson: $"{{\"host\":{System.Text.Json.JsonSerializer.Serialize(vm.HostName)},\"power\":\"{vm.PowerState}\"}}"));
            }
            else
            {
                existing.UpdateMatch(ciId, status, clock.UtcNow);
            }
        }
        await platform.SaveChangesAsync(ct);
        return (vms.Count, matched, 0, unmatched, null);
    }

    public async Task<int> IngestVulnerabilitiesAsync(IReadOnlyList<ScannerVulnerabilityIngestItem> items, CancellationToken ct)
    {
        int created = 0;
        foreach (ScannerVulnerabilityIngestItem item in items)
        {
            created += await security.IngestScannerItemAsync(item, ct) ? 1 : 0;
        }
        return created;
    }

    private static string NormalizeBackupStatus(string status) =>
        status.ToLowerInvariant() switch
        {
            "success" or "ok" or "completed" => "Succeeded",
            "warning" => "Warning",
            "failed" or "error" => "Failed",
            _ => status,
        };

    private static EventSeverity MapSeverity(string status) =>
        status.ToLowerInvariant() switch
        {
            "failed" or "error" or "critical" or "high" or "emergency" => EventSeverity.Critical,
            "warning" or "medium" => EventSeverity.Warning,
            _ => EventSeverity.Info,
        };
}
