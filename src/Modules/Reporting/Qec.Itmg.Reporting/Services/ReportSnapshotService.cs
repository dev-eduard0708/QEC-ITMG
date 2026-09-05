using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Reporting.Domain;
using Qec.Itmg.Reporting.Persistence;

namespace Qec.Itmg.Reporting.Services;

public sealed record ReportSnapshotDto(
    Guid Id,
    string SnapshotKey,
    DateTimeOffset SnapshotDateUtc,
    DateTimeOffset? PeriodStartUtc,
    DateTimeOffset? PeriodEndUtc,
    string PayloadJson,
    DateTimeOffset CreatedAtUtc);

public sealed class ReportSnapshotService(ReportingDbContext db, IClock clock)
{
    public const string ExecutiveKey = "executive.daily";

    public async Task<ReportSnapshotDto> UpsertAsync(
        string snapshotKey,
        DateTimeOffset snapshotDateUtc,
        string payloadJson,
        DateTimeOffset? periodStartUtc,
        DateTimeOffset? periodEndUtc,
        CancellationToken ct)
    {
        DateTimeOffset day = new(snapshotDateUtc.UtcDateTime.Date, TimeSpan.Zero);
        ReportSnapshot? existing = await db.ReportSnapshots
            .FirstOrDefaultAsync(x => x.SnapshotKey == snapshotKey && x.SnapshotDateUtc == day, ct);
        if (existing is not null)
        {
            existing.ReplacePayload(payloadJson, periodStartUtc, periodEndUtc);
            await db.SaveChangesAsync(ct);
            return Map(existing);
        }

        ReportSnapshot entity = ReportSnapshot.Create(
            snapshotKey, day, payloadJson, clock.UtcNow, periodStartUtc, periodEndUtc);
        db.ReportSnapshots.Add(entity);
        await db.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<IReadOnlyList<ReportSnapshotDto>> ListAsync(
        string snapshotKey, int take, CancellationToken ct)
    {
        take = Math.Clamp(take, 1, 90);
        return (await db.ReportSnapshots.AsNoTracking()
            .Where(x => x.SnapshotKey == snapshotKey)
            .OrderByDescending(x => x.SnapshotDateUtc)
            .Take(take)
            .ToListAsync(ct))
            .Select(Map)
            .ToList();
    }

    public async Task<ReportSnapshotDto?> GetLatestAsync(string snapshotKey, CancellationToken ct)
    {
        ReportSnapshot? item = await db.ReportSnapshots.AsNoTracking()
            .Where(x => x.SnapshotKey == snapshotKey)
            .OrderByDescending(x => x.SnapshotDateUtc)
            .FirstOrDefaultAsync(ct);
        return item is null ? null : Map(item);
    }

    private static ReportSnapshotDto Map(ReportSnapshot x) =>
        new(x.Id, x.SnapshotKey, x.SnapshotDateUtc, x.PeriodStartUtc, x.PeriodEndUtc, x.PayloadJson, x.CreatedAtUtc);
}
