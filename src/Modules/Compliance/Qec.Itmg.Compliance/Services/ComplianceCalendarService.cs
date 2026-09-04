using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Compliance.Domain;
using Qec.Itmg.Compliance.Persistence;

namespace Qec.Itmg.Compliance.Services;

public sealed record CalendarItemDto(
    Guid Id, string Title, string ItemType, Guid? InternalControlId, Guid? FrameworkVersionId,
    DateTimeOffset DueAtUtc, Guid? OwnerUserId, string Status, DateTimeOffset? CompletedAtUtc, string? Notes,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, bool IsOverdue);

public sealed class ComplianceCalendarService(ComplianceDbContext db, IClock clock)
{
    public async Task<IReadOnlyList<CalendarItemDto>> ListAsync(
        string? bucket, CancellationToken ct)
    {
        DateTimeOffset now = clock.UtcNow;
        IQueryable<ComplianceCalendarItem> q = db.ComplianceCalendarItems.AsNoTracking();
        List<ComplianceCalendarItem> items = await q.OrderBy(x => x.DueAtUtc).ToListAsync(ct);
        IEnumerable<ComplianceCalendarItem> filtered = bucket?.ToLowerInvariant() switch
        {
            "upcoming" => items.Where(x =>
                x.Status is CalendarItemStatus.Planned or CalendarItemStatus.InProgress && x.DueAtUtc >= now),
            "overdue" => items.Where(x =>
                x.Status is CalendarItemStatus.Planned or CalendarItemStatus.InProgress && x.DueAtUtc < now),
            "completed" => items.Where(x => x.Status == CalendarItemStatus.Completed),
            _ => items,
        };
        return filtered.Select(x => Map(x, now)).ToList();
    }

    public async Task<CalendarItemDto> CreateAsync(
        string title, CalendarItemType itemType, DateTimeOffset dueAtUtc,
        Guid? internalControlId, Guid? frameworkVersionId, Guid? ownerUserId, string? notes, CancellationToken ct)
    {
        ComplianceCalendarItem entity = ComplianceCalendarItem.Create(
            title, itemType, dueAtUtc, clock.UtcNow, internalControlId, frameworkVersionId, ownerUserId, notes);
        db.ComplianceCalendarItems.Add(entity);
        await db.SaveChangesAsync(ct);
        return Map(entity, clock.UtcNow);
    }

    public async Task<CalendarItemDto> UpdateAsync(
        Guid id, string title, DateTimeOffset dueAtUtc, Guid? ownerUserId, string? notes, CancellationToken ct)
    {
        ComplianceCalendarItem entity = await db.ComplianceCalendarItems.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Calendar item was not found.");
        entity.Update(title, dueAtUtc, ownerUserId, notes, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return Map(entity, clock.UtcNow);
    }

    public async Task<CalendarItemDto> SetStatusAsync(Guid id, CalendarItemStatus status, CancellationToken ct)
    {
        ComplianceCalendarItem entity = await db.ComplianceCalendarItems.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Calendar item was not found.");
        entity.SetStatus(status, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return Map(entity, clock.UtcNow);
    }

    /// <summary>Deterministic next due from a frequency label (days). Does not invent complex recurrence.</summary>
    public async Task<CalendarItemDto> ScheduleNextFromFrequencyAsync(
        Guid internalControlId, string title, string frequency, Guid? ownerUserId, CancellationToken ct)
    {
        int days = FrequencyToDays(frequency);
        DateTimeOffset due = clock.UtcNow.AddDays(days);
        return await CreateAsync(
            title, CalendarItemType.ControlAssessment, due, internalControlId, null, ownerUserId, $"Auto from frequency {frequency}", ct);
    }

    private static int FrequencyToDays(string frequency) => frequency.Trim().ToLowerInvariant() switch
    {
        "daily" => 1,
        "weekly" => 7,
        "monthly" => 30,
        "quarterly" => 90,
        "semiannual" => 182,
        "annual" => 365,
        "continuous" => 30,
        "eventdriven" => 30,
        "adhoc" => 30,
        _ => 90,
    };

    private static CalendarItemDto Map(ComplianceCalendarItem x, DateTimeOffset now) => new(
        x.Id, x.Title, x.ItemType.ToString(), x.InternalControlId, x.FrameworkVersionId,
        x.DueAtUtc, x.OwnerUserId, x.Status.ToString(), x.CompletedAtUtc, x.Notes,
        x.CreatedAtUtc, x.UpdatedAtUtc,
        x.Status is CalendarItemStatus.Planned or CalendarItemStatus.InProgress && x.DueAtUtc < now);
}
