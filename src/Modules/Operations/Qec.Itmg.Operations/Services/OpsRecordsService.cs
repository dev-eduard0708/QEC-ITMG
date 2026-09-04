using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Operations.Domain;
using Qec.Itmg.Operations.Persistence;

namespace Qec.Itmg.Operations.Services;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

public sealed record BackupJobDto(
    Guid Id, string Name, string Provider, string? ExternalJobId, Guid? ConfigurationItemId,
    bool IsActive, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);

public sealed record BackupRunDto(
    Guid Id, Guid BackupJobId, DateTimeOffset StartedAtUtc, DateTimeOffset? CompletedAtUtc,
    string Status, string? Summary, string? ExternalReference);

public sealed record RestoreTestDto(
    Guid Id, Guid? BackupJobId, Guid? ConfigurationItemId, DateTimeOffset? ScheduledAtUtc,
    DateTimeOffset? PerformedAtUtc, string Result, Guid? PerformedByUserId, string? Notes, DateTimeOffset CreatedAtUtc);

public sealed record CertificateDto(
    Guid Id, string Name, Guid? ConfigurationItemId, string? Subject, string? Issuer, string? Thumbprint,
    DateTimeOffset ExpiresAtUtc, Guid? OwnerUserId, bool IsActive,
    int DaysToExpiry, bool Expired, bool ExpiringSoon,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);

public sealed record PatchBaselineDto(
    Guid Id, string Name, string? Description, string? Version, bool IsActive,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);

public sealed record PatchDeploymentDto(
    Guid Id, Guid? PatchBaselineId, Guid ConfigurationItemId, string? ExternalReference, string Status,
    DateTimeOffset? ScheduledAtUtc, DateTimeOffset? StartedAtUtc, DateTimeOffset? CompletedAtUtc,
    string? Summary, DateTimeOffset CreatedAtUtc);

public sealed record ScheduledJobDto(
    Guid Id, string Name, string? Provider, string? ExternalJobId, Guid? ConfigurationItemId,
    string? ScheduleDescription, bool IsActive, DateTimeOffset? LastRunAtUtc, string LastResult,
    DateTimeOffset? NextRunAtUtc, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);

public sealed class OpsRecordsService(OperationsDbContext db, IClock clock)
{
    private static (int page, int pageSize) NormalizePage(int page, int pageSize) =>
        (Math.Max(1, page), Math.Clamp(pageSize, 1, 100));

    // --- Backup jobs ---
    public async Task<PagedResult<BackupJobDto>> ListBackupJobsAsync(int page, int pageSize, string? search, CancellationToken ct)
    {
        (page, pageSize) = NormalizePage(page, pageSize);
        IQueryable<BackupJob> q = db.BackupJobs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            q = q.Where(x => x.Name.Contains(term) || x.Provider.Contains(term));
        }

        int total = await q.CountAsync(ct);
        List<BackupJob> items = await q.OrderBy(x => x.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(items.Select(Map).ToList(), total, page, pageSize);
    }

    public async Task<BackupJobDto?> GetBackupJobAsync(Guid id, CancellationToken ct)
    {
        BackupJob? item = await db.BackupJobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? null : Map(item);
    }

    public async Task<BackupJobDto> CreateBackupJobAsync(string name, string provider, string? externalJobId, Guid? configurationItemId, CancellationToken ct)
    {
        BackupJob entity = BackupJob.Create(name, provider, clock.UtcNow, externalJobId, configurationItemId);
        db.BackupJobs.Add(entity);
        await db.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<BackupJobDto> UpdateBackupJobAsync(Guid id, string name, string provider, string? externalJobId, Guid? configurationItemId, bool isActive, CancellationToken ct)
    {
        BackupJob entity = await db.BackupJobs.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Backup job not found.");
        entity.Update(name, provider, externalJobId, configurationItemId, isActive, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return Map(entity);
    }

    // --- Backup runs ---
    public async Task<PagedResult<BackupRunDto>> ListBackupRunsAsync(int page, int pageSize, Guid? backupJobId, string? status, CancellationToken ct)
    {
        (page, pageSize) = NormalizePage(page, pageSize);
        IQueryable<BackupRun> q = db.BackupRuns.AsNoTracking();
        if (backupJobId is Guid jobId) q = q.Where(x => x.BackupJobId == jobId);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse(status, true, out BackupRunStatus parsed))
            q = q.Where(x => x.Status == parsed);

        int total = await q.CountAsync(ct);
        List<BackupRun> items = await q.OrderByDescending(x => x.StartedAtUtc).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(items.Select(Map).ToList(), total, page, pageSize);
    }

    public async Task<BackupRunDto?> GetBackupRunAsync(Guid id, CancellationToken ct)
    {
        BackupRun? item = await db.BackupRuns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? null : Map(item);
    }

    public async Task<BackupRunDto> CreateBackupRunAsync(Guid backupJobId, DateTimeOffset startedAtUtc, string status, string? summary, string? externalReference, DateTimeOffset? completedAtUtc, CancellationToken ct)
    {
        if (!await db.BackupJobs.AnyAsync(x => x.Id == backupJobId, ct))
            throw new InvalidOperationException("Backup job not found.");
        if (!Enum.TryParse(status, true, out BackupRunStatus parsed))
            throw new ArgumentException("A valid status is required.", nameof(status));
        BackupRun entity = BackupRun.Create(backupJobId, startedAtUtc, parsed, summary, externalReference, completedAtUtc);
        db.BackupRuns.Add(entity);
        await db.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<BackupRunDto> UpdateBackupRunAsync(Guid id, string status, DateTimeOffset? completedAtUtc, string? summary, string? externalReference, CancellationToken ct)
    {
        BackupRun entity = await db.BackupRuns.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Backup run not found.");
        if (!Enum.TryParse(status, true, out BackupRunStatus parsed))
            throw new ArgumentException("A valid status is required.", nameof(status));
        entity.Update(parsed, completedAtUtc, summary, externalReference);
        await db.SaveChangesAsync(ct);
        return Map(entity);
    }

    // --- Restore tests ---
    public async Task<PagedResult<RestoreTestDto>> ListRestoreTestsAsync(int page, int pageSize, string? result, CancellationToken ct)
    {
        (page, pageSize) = NormalizePage(page, pageSize);
        IQueryable<RestoreTest> q = db.RestoreTests.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(result) && Enum.TryParse(result, true, out RestoreTestResult parsed))
            q = q.Where(x => x.Result == parsed);
        int total = await q.CountAsync(ct);
        List<RestoreTest> items = await q.OrderByDescending(x => x.CreatedAtUtc).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(items.Select(Map).ToList(), total, page, pageSize);
    }

    public async Task<RestoreTestDto?> GetRestoreTestAsync(Guid id, CancellationToken ct)
    {
        RestoreTest? item = await db.RestoreTests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? null : Map(item);
    }

    public async Task<RestoreTestDto> CreateRestoreTestAsync(Guid? backupJobId, Guid? configurationItemId, DateTimeOffset? scheduledAtUtc, string? notes, CancellationToken ct)
    {
        RestoreTest entity = RestoreTest.Create(clock.UtcNow, backupJobId, configurationItemId, scheduledAtUtc, notes);
        db.RestoreTests.Add(entity);
        await db.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<RestoreTestDto> UpdateRestoreTestAsync(Guid id, Guid? backupJobId, Guid? configurationItemId, DateTimeOffset? scheduledAtUtc, DateTimeOffset? performedAtUtc, string result, Guid? performedByUserId, string? notes, CancellationToken ct)
    {
        RestoreTest entity = await db.RestoreTests.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Restore test not found.");
        if (!Enum.TryParse(result, true, out RestoreTestResult parsed))
            throw new ArgumentException("A valid result is required.", nameof(result));
        entity.Update(backupJobId, configurationItemId, scheduledAtUtc, performedAtUtc, parsed, performedByUserId, notes);
        await db.SaveChangesAsync(ct);
        return Map(entity);
    }

    // --- Certificates ---
    public async Task<PagedResult<CertificateDto>> ListCertificatesAsync(int page, int pageSize, string? search, bool? activeOnly, CancellationToken ct)
    {
        (page, pageSize) = NormalizePage(page, pageSize);
        DateTimeOffset now = clock.UtcNow;
        IQueryable<CertificateRecord> q = db.CertificateRecords.AsNoTracking();
        if (activeOnly == true) q = q.Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            q = q.Where(x => x.Name.Contains(term) || (x.Subject != null && x.Subject.Contains(term)) || (x.Thumbprint != null && x.Thumbprint.Contains(term)));
        }

        int total = await q.CountAsync(ct);
        List<CertificateRecord> items = await q.OrderBy(x => x.ExpiresAtUtc).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(items.Select(x => Map(x, now)).ToList(), total, page, pageSize);
    }

    public async Task<CertificateDto?> GetCertificateAsync(Guid id, CancellationToken ct)
    {
        CertificateRecord? item = await db.CertificateRecords.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? null : Map(item, clock.UtcNow);
    }

    public async Task<CertificateDto> CreateCertificateAsync(string name, DateTimeOffset expiresAtUtc, Guid? configurationItemId, string? subject, string? issuer, string? thumbprint, Guid? ownerUserId, CancellationToken ct)
    {
        CertificateRecord entity = CertificateRecord.Create(name, expiresAtUtc, clock.UtcNow, configurationItemId, subject, issuer, thumbprint, ownerUserId);
        db.CertificateRecords.Add(entity);
        await db.SaveChangesAsync(ct);
        return Map(entity, clock.UtcNow);
    }

    public async Task<CertificateDto> UpdateCertificateAsync(Guid id, string name, DateTimeOffset expiresAtUtc, Guid? configurationItemId, string? subject, string? issuer, string? thumbprint, Guid? ownerUserId, bool isActive, CancellationToken ct)
    {
        CertificateRecord entity = await db.CertificateRecords.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Certificate not found.");
        entity.Update(name, expiresAtUtc, configurationItemId, subject, issuer, thumbprint, ownerUserId, isActive, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return Map(entity, clock.UtcNow);
    }

    // --- Patch baselines ---
    public async Task<PagedResult<PatchBaselineDto>> ListPatchBaselinesAsync(int page, int pageSize, string? search, CancellationToken ct)
    {
        (page, pageSize) = NormalizePage(page, pageSize);
        IQueryable<PatchBaseline> q = db.PatchBaselines.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            q = q.Where(x => x.Name.Contains(term));
        }

        int total = await q.CountAsync(ct);
        List<PatchBaseline> items = await q.OrderBy(x => x.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(items.Select(Map).ToList(), total, page, pageSize);
    }

    public async Task<PatchBaselineDto?> GetPatchBaselineAsync(Guid id, CancellationToken ct)
    {
        PatchBaseline? item = await db.PatchBaselines.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? null : Map(item);
    }

    public async Task<PatchBaselineDto> CreatePatchBaselineAsync(string name, string? description, string? version, CancellationToken ct)
    {
        PatchBaseline entity = PatchBaseline.Create(name, clock.UtcNow, description, version);
        db.PatchBaselines.Add(entity);
        await db.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<PatchBaselineDto> UpdatePatchBaselineAsync(Guid id, string name, string? description, string? version, bool isActive, CancellationToken ct)
    {
        PatchBaseline entity = await db.PatchBaselines.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Patch baseline not found.");
        entity.Update(name, description, version, isActive, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return Map(entity);
    }

    // --- Patch deployments ---
    public async Task<PagedResult<PatchDeploymentDto>> ListPatchDeploymentsAsync(int page, int pageSize, Guid? configurationItemId, string? status, CancellationToken ct)
    {
        (page, pageSize) = NormalizePage(page, pageSize);
        IQueryable<PatchDeployment> q = db.PatchDeployments.AsNoTracking();
        if (configurationItemId is Guid ci) q = q.Where(x => x.ConfigurationItemId == ci);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse(status, true, out PatchDeploymentStatus parsed))
            q = q.Where(x => x.Status == parsed);
        int total = await q.CountAsync(ct);
        List<PatchDeployment> items = await q.OrderByDescending(x => x.CreatedAtUtc).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(items.Select(Map).ToList(), total, page, pageSize);
    }

    public async Task<PatchDeploymentDto?> GetPatchDeploymentAsync(Guid id, CancellationToken ct)
    {
        PatchDeployment? item = await db.PatchDeployments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? null : Map(item);
    }

    public async Task<PatchDeploymentDto> CreatePatchDeploymentAsync(Guid configurationItemId, Guid? patchBaselineId, string? externalReference, DateTimeOffset? scheduledAtUtc, string? summary, CancellationToken ct)
    {
        PatchDeployment entity = PatchDeployment.Create(configurationItemId, clock.UtcNow, patchBaselineId, externalReference, scheduledAtUtc, summary);
        db.PatchDeployments.Add(entity);
        await db.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<PatchDeploymentDto> UpdatePatchDeploymentAsync(Guid id, Guid? patchBaselineId, Guid configurationItemId, string? externalReference, string status, DateTimeOffset? scheduledAtUtc, DateTimeOffset? startedAtUtc, DateTimeOffset? completedAtUtc, string? summary, CancellationToken ct)
    {
        PatchDeployment entity = await db.PatchDeployments.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Patch deployment not found.");
        if (!Enum.TryParse(status, true, out PatchDeploymentStatus parsed))
            throw new ArgumentException("A valid status is required.", nameof(status));
        entity.Update(patchBaselineId, configurationItemId, externalReference, parsed, scheduledAtUtc, startedAtUtc, completedAtUtc, summary);
        await db.SaveChangesAsync(ct);
        return Map(entity);
    }

    // --- Scheduled jobs ---
    public async Task<PagedResult<ScheduledJobDto>> ListScheduledJobsAsync(int page, int pageSize, string? search, CancellationToken ct)
    {
        (page, pageSize) = NormalizePage(page, pageSize);
        IQueryable<ScheduledJob> q = db.ScheduledJobs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            q = q.Where(x => x.Name.Contains(term) || (x.Provider != null && x.Provider.Contains(term)));
        }

        int total = await q.CountAsync(ct);
        List<ScheduledJob> items = await q.OrderBy(x => x.NextRunAtUtc).ThenBy(x => x.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(items.Select(Map).ToList(), total, page, pageSize);
    }

    public async Task<ScheduledJobDto?> GetScheduledJobAsync(Guid id, CancellationToken ct)
    {
        ScheduledJob? item = await db.ScheduledJobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? null : Map(item);
    }

    public async Task<ScheduledJobDto> CreateScheduledJobAsync(string name, string? provider, string? externalJobId, Guid? configurationItemId, string? scheduleDescription, DateTimeOffset? nextRunAtUtc, CancellationToken ct)
    {
        ScheduledJob entity = ScheduledJob.Create(name, clock.UtcNow, provider, externalJobId, configurationItemId, scheduleDescription, nextRunAtUtc);
        db.ScheduledJobs.Add(entity);
        await db.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<ScheduledJobDto> UpdateScheduledJobAsync(Guid id, string name, string? provider, string? externalJobId, Guid? configurationItemId, string? scheduleDescription, bool isActive, DateTimeOffset? lastRunAtUtc, string lastResult, DateTimeOffset? nextRunAtUtc, CancellationToken ct)
    {
        ScheduledJob entity = await db.ScheduledJobs.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Scheduled job not found.");
        if (!Enum.TryParse(lastResult, true, out JobLastResult parsed))
            throw new ArgumentException("A valid lastResult is required.", nameof(lastResult));
        entity.Update(name, provider, externalJobId, configurationItemId, scheduleDescription, isActive, lastRunAtUtc, parsed, nextRunAtUtc, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return Map(entity);
    }

    private static BackupJobDto Map(BackupJob x) =>
        new(x.Id, x.Name, x.Provider, x.ExternalJobId, x.ConfigurationItemId, x.IsActive, x.CreatedAtUtc, x.UpdatedAtUtc);

    private static BackupRunDto Map(BackupRun x) =>
        new(x.Id, x.BackupJobId, x.StartedAtUtc, x.CompletedAtUtc, x.Status.ToString(), x.Summary, x.ExternalReference);

    private static RestoreTestDto Map(RestoreTest x) =>
        new(x.Id, x.BackupJobId, x.ConfigurationItemId, x.ScheduledAtUtc, x.PerformedAtUtc, x.Result.ToString(), x.PerformedByUserId, x.Notes, x.CreatedAtUtc);

    private static CertificateDto Map(CertificateRecord x, DateTimeOffset now) =>
        new(x.Id, x.Name, x.ConfigurationItemId, x.Subject, x.Issuer, x.Thumbprint, x.ExpiresAtUtc, x.OwnerUserId, x.IsActive,
            x.DaysToExpiry(now), x.IsExpired(now), x.IsExpiringSoon(now), x.CreatedAtUtc, x.UpdatedAtUtc);

    private static PatchBaselineDto Map(PatchBaseline x) =>
        new(x.Id, x.Name, x.Description, x.Version, x.IsActive, x.CreatedAtUtc, x.UpdatedAtUtc);

    private static PatchDeploymentDto Map(PatchDeployment x) =>
        new(x.Id, x.PatchBaselineId, x.ConfigurationItemId, x.ExternalReference, x.Status.ToString(),
            x.ScheduledAtUtc, x.StartedAtUtc, x.CompletedAtUtc, x.Summary, x.CreatedAtUtc);

    private static ScheduledJobDto Map(ScheduledJob x) =>
        new(x.Id, x.Name, x.Provider, x.ExternalJobId, x.ConfigurationItemId, x.ScheduleDescription, x.IsActive,
            x.LastRunAtUtc, x.LastResult.ToString(), x.NextRunAtUtc, x.CreatedAtUtc, x.UpdatedAtUtc);
}

public sealed class CertificateExpiryService(
    OperationsDbContext db,
    IClock clock)
{
    public static readonly int[] Thresholds = [30, 14, 7, 1, 0];

    public async Task<IReadOnlyList<CertificateExpiryCandidate>> FindDueNotificationsAsync(CancellationToken ct = default)
    {
        DateTimeOffset now = clock.UtcNow;
        List<CertificateRecord> certs = await db.CertificateRecords.AsNoTracking()
            .Where(x => x.IsActive)
            .ToListAsync(ct);

        HashSet<(Guid, int)> already = (await db.CertificateExpiryNotificationLogs.AsNoTracking()
            .Select(x => new { x.CertificateId, x.ThresholdDays })
            .ToListAsync(ct))
            .Select(x => (x.CertificateId, x.ThresholdDays))
            .ToHashSet();

        List<CertificateExpiryCandidate> due = [];
        foreach (CertificateRecord cert in certs)
        {
            int days = cert.DaysToExpiry(now);
            foreach (int threshold in Thresholds)
            {
                bool crossed = threshold == 0
                    ? cert.IsExpired(now)
                    : !cert.IsExpired(now) && days <= threshold;
                if (!crossed || already.Contains((cert.Id, threshold))) continue;
                due.Add(new CertificateExpiryCandidate(cert.Id, cert.Name, cert.OwnerUserId, cert.ExpiresAtUtc, days, threshold));
            }
        }

        return due;
    }

    public async Task MarkNotifiedAsync(Guid certificateId, int thresholdDays, CancellationToken ct = default)
    {
        bool exists = await db.CertificateExpiryNotificationLogs
            .AnyAsync(x => x.CertificateId == certificateId && x.ThresholdDays == thresholdDays, ct);
        if (exists) return;
        db.CertificateExpiryNotificationLogs.Add(CertificateExpiryNotificationLog.Create(certificateId, thresholdDays, clock.UtcNow));
        await db.SaveChangesAsync(ct);
    }
}

public sealed record CertificateExpiryCandidate(
    Guid CertificateId,
    string Name,
    Guid? OwnerUserId,
    DateTimeOffset ExpiresAtUtc,
    int DaysToExpiry,
    int ThresholdDays);

public sealed class EventRetentionService(
    OperationsDbContext db,
    IClock clock,
    IOptions<OperationsOptions> options,
    ILogger<EventRetentionService> logger)
{
    public async Task<int> PurgeClosedEventsAsync(CancellationToken ct = default)
    {
        OperationsOptions opts = options.Value;
        int closedDays = Math.Max(1, opts.ClosedEventRetentionDays);
        int maxDays = Math.Max(closedDays, Math.Max(1, opts.EventRetentionDays));
        DateTimeOffset cutoff = clock.UtcNow.AddDays(-closedDays);
        DateTimeOffset absoluteCutoff = clock.UtcNow.AddDays(-maxDays);

        // Only Closed; never New/Acknowledged/Promoted. Linked tickets / audit live elsewhere.
        List<OperationalEvent> eligible = await db.OperationalEvents
            .Where(x => x.Status == EventStatus.Closed
                && (x.UpdatedAtUtc < cutoff || x.UpdatedAtUtc < absoluteCutoff))
            .OrderBy(x => x.UpdatedAtUtc)
            .Take(500)
            .ToListAsync(ct);

        if (eligible.Count == 0)
        {
            logger.LogInformation("Event retention: no closed events eligible for purge (closedDays={ClosedDays}).", closedDays);
            return 0;
        }

        db.OperationalEvents.RemoveRange(eligible);
        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "Event retention: purged {Count} closed operational events older than {Cutoff:o}.",
            eligible.Count,
            cutoff);
        return eligible.Count;
    }
}
