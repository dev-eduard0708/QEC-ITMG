using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Platform.Domain;
using Qec.Itmg.Platform.Persistence;

namespace Qec.Itmg.Platform.Integrations;

public sealed record IntegrationRunDto(
    Guid Id,
    string Provider,
    string Operation,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string Status,
    int ProcessedCount,
    int SucceededCount,
    int FailedCount,
    int UnmatchedCount,
    string? ErrorSummary,
    string CorrelationId);

public sealed class IntegrationRunService(PlatformDbContext db, IClock clock)
{
    public async Task<IntegrationRun> StartAsync(string provider, string operation, CancellationToken ct, string? correlationId = null)
    {
        IntegrationRun run = IntegrationRun.Start(provider, operation, clock.UtcNow, correlationId);
        db.IntegrationRuns.Add(run);
        await db.SaveChangesAsync(ct);
        return run;
    }

    public async Task CompleteAsync(
        IntegrationRun run,
        IntegrationRunStatus status,
        int processed,
        int succeeded,
        int failed,
        int unmatched,
        string? errorSummary,
        CancellationToken ct)
    {
        run.Complete(status, clock.UtcNow, processed, succeeded, failed, unmatched, errorSummary);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<IntegrationRunDto>> ListAsync(string? provider, int take, CancellationToken ct)
    {
        take = Math.Clamp(take, 1, 100);
        IQueryable<IntegrationRun> q = db.IntegrationRuns.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(provider))
            q = q.Where(x => x.Provider == provider.Trim());
        return await q.OrderByDescending(x => x.StartedAtUtc)
            .Take(take)
            .Select(x => new IntegrationRunDto(
                x.Id, x.Provider, x.Operation, x.StartedAtUtc, x.CompletedAtUtc, x.Status.ToString(),
                x.ProcessedCount, x.SucceededCount, x.FailedCount, x.UnmatchedCount, x.ErrorSummary, x.CorrelationId))
            .ToListAsync(ct);
    }

    public async Task<bool> HasRunningAsync(string provider, string operation, CancellationToken ct) =>
        await db.IntegrationRuns.AnyAsync(
            x => x.Provider == provider && x.Operation == operation && x.Status == IntegrationRunStatus.Running
                 && x.StartedAtUtc > clock.UtcNow.AddHours(-2),
            ct);
}
