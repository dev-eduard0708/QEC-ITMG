using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Contracts.Numbering;
using Qec.Itmg.Operations.Domain;
using Qec.Itmg.Operations.Persistence;

namespace Qec.Itmg.Operations.Services;

public sealed record EventDto(
    Guid Id,
    string EventNumber,
    string Source,
    string SourceEventKey,
    string Severity,
    string Title,
    string Summary,
    Guid? ConfigurationItemId,
    string Status,
    int OccurrenceCount,
    DateTimeOffset FirstSeenAtUtc,
    DateTimeOffset LastSeenAtUtc,
    DateTimeOffset? AcknowledgedAtUtc,
    Guid? AcknowledgedByUserId,
    Guid? LinkedTicketId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string RowVersion);

public sealed record EventListResult(IReadOnlyList<EventDto> Items, int TotalCount, int Page, int PageSize);

public sealed record IngestResult(EventDto Event, bool Created);

internal static class EventAuditComposer
{
    public static BusinessAuditEntry Created(Guid id, string number) =>
        new()
        {
            AggregateType = AuditAggregateType.Event,
            AggregateId = id,
            BusinessNumber = number,
            Action = BusinessAuditAction.Created,
            Source = AuditSource.Api,
        };

    public static BusinessAuditEntry Field(
        Guid id,
        string? number,
        string fieldName,
        string? oldValue,
        string? newValue,
        BusinessAuditAction action = BusinessAuditAction.Updated) =>
        new()
        {
            AggregateType = AuditAggregateType.Event,
            AggregateId = id,
            BusinessNumber = number,
            Action = action,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue,
            Source = AuditSource.Api,
        };
}

public sealed class EventService(
    OperationsDbContext db,
    INumberSequenceService numbers,
    IClock clock,
    IBusinessAuditWriter businessAudit,
    ISharedDbTransaction sharedDbTransaction)
{
    public const string SequenceKey = "events";
    public const string Prefix = "EVT";

    public async Task<EventListResult> ListAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        EventStatus? status = null,
        EventSeverity? severity = null,
        string? source = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        IQueryable<OperationalEvent> query = db.OperationalEvents.AsNoTracking();
        if (status is EventStatus s) query = query.Where(item => item.Status == s);
        if (severity is EventSeverity sev) query = query.Where(item => item.Severity == sev);
        if (!string.IsNullOrWhiteSpace(source))
        {
            string src = source.Trim();
            query = query.Where(item => item.Source == src);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            query = query.Where(item =>
                item.Title.Contains(term)
                || item.EventNumber.Contains(term)
                || item.Summary.Contains(term)
                || item.SourceEventKey.Contains(term));
        }

        int total = await query.CountAsync(cancellationToken);
        List<OperationalEvent> items = await query
            .OrderByDescending(item => item.LastSeenAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new EventListResult(items.Select(Map).ToList(), total, page, pageSize);
    }

    public async Task<EventDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        OperationalEvent? item = await db.OperationalEvents.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        return item is null ? null : Map(item);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        await db.OperationalEvents.AsNoTracking().AnyAsync(e => e.Id == id, cancellationToken);

    public async Task<IngestResult> IngestAsync(
        string source,
        string sourceEventKey,
        EventSeverity severity,
        string title,
        string summary,
        Guid? configurationItemId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEventKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);

        string normalizedSource = source.Trim();
        string normalizedKey = sourceEventKey.Trim();

        OperationalEvent? existing = await db.OperationalEvents.FirstOrDefaultAsync(
            item => item.Source == normalizedSource && item.SourceEventKey == normalizedKey,
            cancellationToken);

        if (existing is not null)
        {
            int before = existing.OccurrenceCount;
            existing.RecordOccurrence(severity, title, summary, configurationItemId, clock.UtcNow);
            await sharedDbTransaction.ExecuteAsync(
                async ct =>
                {
                    await businessAudit.AppendAsync(
                        EventAuditComposer.Field(
                            existing.Id,
                            existing.EventNumber,
                            "OccurrenceCount",
                            before.ToString(),
                            existing.OccurrenceCount.ToString(),
                            BusinessAuditAction.Updated),
                        ct);
                    await db.SaveChangesAsync(ct);
                },
                cancellationToken);
            return new IngestResult(Map(existing), Created: false);
        }

        string number = await numbers.NextAsync(SequenceKey, Prefix, cancellationToken);
        OperationalEvent created = OperationalEvent.Create(
            number, normalizedSource, normalizedKey, severity, title, summary, clock.UtcNow, configurationItemId);

        await sharedDbTransaction.ExecuteAsync(
            async ct =>
            {
                db.OperationalEvents.Add(created);
                await businessAudit.AppendAsync(EventAuditComposer.Created(created.Id, created.EventNumber), ct);
                await db.SaveChangesAsync(ct);
            },
            cancellationToken);

        return new IngestResult(Map(created), Created: true);
    }

    public async Task<EventDto> AcknowledgeAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        OperationalEvent item = await db.OperationalEvents.FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Event was not found.");
        string before = item.Status.ToString();
        item.Acknowledge(userId, clock.UtcNow);
        await sharedDbTransaction.ExecuteAsync(
            async ct =>
            {
                await businessAudit.AppendAsync(
                    EventAuditComposer.Field(
                        item.Id, item.EventNumber, "Status", before, item.Status.ToString(), BusinessAuditAction.StatusChanged),
                    ct);
                await db.SaveChangesAsync(ct);
            },
            cancellationToken);
        return Map(item);
    }

    public async Task<EventDto> MarkPromotedAsync(Guid id, Guid ticketId, CancellationToken cancellationToken = default)
    {
        OperationalEvent item = await db.OperationalEvents.FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Event was not found.");
        string before = item.Status.ToString();
        item.MarkPromoted(ticketId, clock.UtcNow);
        await sharedDbTransaction.ExecuteAsync(
            async ct =>
            {
                await businessAudit.AppendAsync(
                    EventAuditComposer.Field(
                        item.Id, item.EventNumber, "Status", before, item.Status.ToString(), BusinessAuditAction.StatusChanged),
                    ct);
                await businessAudit.AppendAsync(
                    EventAuditComposer.Field(
                        item.Id, item.EventNumber, "LinkedTicketId", null, ticketId.ToString("D"), BusinessAuditAction.Linked),
                    ct);
                await db.SaveChangesAsync(ct);
            },
            cancellationToken);
        return Map(item);
    }

    public async Task<EventDto> CloseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        OperationalEvent item = await db.OperationalEvents.FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Event was not found.");
        string before = item.Status.ToString();
        item.Close(clock.UtcNow);
        await sharedDbTransaction.ExecuteAsync(
            async ct =>
            {
                await businessAudit.AppendAsync(
                    EventAuditComposer.Field(
                        item.Id, item.EventNumber, "Status", before, item.Status.ToString(), BusinessAuditAction.StatusChanged),
                    ct);
                await db.SaveChangesAsync(ct);
            },
            cancellationToken);
        return Map(item);
    }

    private static EventDto Map(OperationalEvent item) =>
        new(
            item.Id,
            item.EventNumber,
            item.Source,
            item.SourceEventKey,
            item.Severity.ToString(),
            item.Title,
            item.Summary,
            item.ConfigurationItemId,
            item.Status.ToString(),
            item.OccurrenceCount,
            item.FirstSeenAtUtc,
            item.LastSeenAtUtc,
            item.AcknowledgedAtUtc,
            item.AcknowledgedByUserId,
            item.LinkedTicketId,
            item.CreatedAtUtc,
            item.UpdatedAtUtc,
            Convert.ToBase64String(item.RowVersion));
}
