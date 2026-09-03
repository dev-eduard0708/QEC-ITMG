using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Numbering;
using Qec.Itmg.ServiceDesk.Domain;
using Qec.Itmg.ServiceDesk.Persistence;

namespace Qec.Itmg.ServiceDesk.Services;

public sealed record TicketDto(
    Guid Id,
    string TicketNumber,
    string Type,
    string Title,
    string Description,
    string Status,
    string Priority,
    Guid RequesterUserId,
    Guid? AssignedUserId,
    Guid? QueueId,
    Guid? ConfigurationItemId,
    string? Category,
    Guid? SlaPolicyId,
    DateTimeOffset? ResponseDueAtUtc,
    DateTimeOffset? ResolutionDueAtUtc,
    DateTimeOffset? RespondedAtUtc,
    bool ResponseBreached,
    bool ResolutionBreached,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ResolvedAtUtc,
    DateTimeOffset? ClosedAtUtc,
    string RowVersion);

public sealed record SupportQueueDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive);

public sealed record TicketListResult(
    IReadOnlyList<TicketDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed class TicketService(
    ServiceDeskDbContext db,
    INumberSequenceService numbers,
    IClock clock)
{
    public const string IncidentSequenceKey = "tickets-incident";
    public const string ServiceRequestSequenceKey = "tickets-service-request";
    public const string IncidentPrefix = "INC";
    public const string ServiceRequestPrefix = "SR";

    public async Task<TicketListResult> ListAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        TicketStatus? status = null,
        TicketType? type = null,
        TicketPriority? priority = null,
        Guid? requesterUserId = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        IQueryable<Ticket> query = db.Tickets.AsNoTracking();
        if (requesterUserId is Guid requester)
        {
            query = query.Where(item => item.RequesterUserId == requester);
        }

        if (status is TicketStatus statusFilter)
        {
            query = query.Where(item => item.Status == statusFilter);
        }

        if (type is TicketType typeFilter)
        {
            query = query.Where(item => item.Type == typeFilter);
        }

        if (priority is TicketPriority priorityFilter)
        {
            query = query.Where(item => item.Priority == priorityFilter);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            query = query.Where(item =>
                item.Title.Contains(term)
                || item.TicketNumber.Contains(term)
                || item.Description.Contains(term));
        }

        int total = await query.CountAsync(cancellationToken);
        List<Ticket> items = await query
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new TicketListResult(items.Select(Map).ToList(), total, page, pageSize);
    }

    public async Task<TicketDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Ticket? ticket = await db.Tickets.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return ticket is null ? null : Map(ticket);
    }

    public async Task<TicketDto?> GetForRequesterAsync(
        Guid id,
        Guid requesterUserId,
        CancellationToken cancellationToken = default)
    {
        Ticket? ticket = await db.Tickets.AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == id && item.RequesterUserId == requesterUserId,
                cancellationToken);
        return ticket is null ? null : Map(ticket);
    }

    public async Task<Ticket> CreateAsync(
        TicketType type,
        string title,
        string description,
        Guid requesterUserId,
        TicketPriority priority = TicketPriority.Medium,
        Guid? configurationItemId = null,
        string? category = null,
        Guid? queueId = null,
        CancellationToken cancellationToken = default)
    {
        if (queueId is Guid qid)
        {
            await EnsureQueueExistsAsync(qid, cancellationToken);
        }

        string sequenceKey = type == TicketType.Incident ? IncidentSequenceKey : ServiceRequestSequenceKey;
        string prefix = type == TicketType.Incident ? IncidentPrefix : ServiceRequestPrefix;
        string ticketNumber = await numbers.NextAsync(sequenceKey, prefix, cancellationToken);

        DateTimeOffset utcNow = clock.UtcNow;
        Ticket ticket = Ticket.Create(
            ticketNumber,
            type,
            title,
            description,
            requesterUserId,
            priority,
            utcNow,
            configurationItemId,
            category,
            queueId);

        await ApplyMatchingSlaAsync(ticket, utcNow, cancellationToken);

        db.Tickets.Add(ticket);
        await db.SaveChangesAsync(cancellationToken);
        return ticket;
    }

    public async Task<Ticket> UpdateAsync(
        Guid id,
        string title,
        string description,
        TicketPriority priority,
        Guid? configurationItemId,
        string? category,
        string rowVersion,
        CancellationToken cancellationToken = default)
    {
        Ticket ticket = await db.Tickets.FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Ticket was not found.");

        ticket.UpdateDetails(
            title,
            description,
            priority,
            configurationItemId,
            category,
            rowVersion,
            clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return ticket;
    }

    public async Task<Ticket> ChangeStatusAsync(
        Guid id,
        TicketStatus status,
        string? rowVersion = null,
        CancellationToken cancellationToken = default)
    {
        Ticket ticket = await db.Tickets.FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Ticket was not found.");

        ticket.ChangeStatus(status, clock.UtcNow, rowVersion);
        await db.SaveChangesAsync(cancellationToken);
        return ticket;
    }

    public async Task<Ticket> AssignAsync(
        Guid id,
        Guid assignedByUserId,
        Guid? queueId,
        Guid? assignedUserId,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        Ticket ticket = await db.Tickets.FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Ticket was not found.");

        if (queueId is Guid qid)
        {
            await EnsureQueueExistsAsync(qid, cancellationToken);
        }

        ticket.Assign(queueId, assignedUserId, clock.UtcNow);

        db.TicketAssignmentHistories.Add(
            TicketAssignmentHistory.Create(
                ticket.Id,
                assignedByUserId,
                clock.UtcNow,
                ticket.QueueId,
                ticket.AssignedUserId,
                notes));

        await db.SaveChangesAsync(cancellationToken);
        return ticket;
    }

    public async Task<IReadOnlyList<SupportQueueDto>> ListQueuesAsync(CancellationToken cancellationToken = default)
    {
        return await db.SupportQueues.AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.Name)
            .Select(item => new SupportQueueDto(item.Id, item.Name, item.Description, item.IsActive))
            .ToListAsync(cancellationToken);
    }

    private async Task ApplyMatchingSlaAsync(Ticket ticket, DateTimeOffset utcNow, CancellationToken cancellationToken)
    {
        List<SlaPolicy> policies = await db.SlaPolicies.AsNoTracking()
            .Where(item => item.IsActive && item.Priority == ticket.Priority)
            .ToListAsync(cancellationToken);

        SlaPolicy? match = policies
            .Where(item => item.TicketType is null || item.TicketType == ticket.Type)
            .OrderByDescending(item => item.TicketType is null ? 0 : 1)
            .ThenBy(item => item.Name)
            .FirstOrDefault();

        if (match is null)
        {
            return;
        }

        ticket.ApplySla(
            match.Id,
            utcNow.AddMinutes(match.ResponseMinutes),
            utcNow.AddMinutes(match.ResolutionMinutes),
            utcNow);
    }

    private async Task EnsureQueueExistsAsync(Guid queueId, CancellationToken cancellationToken)
    {
        bool exists = await db.SupportQueues.AsNoTracking()
            .AnyAsync(item => item.Id == queueId && item.IsActive, cancellationToken);
        if (!exists)
        {
            throw new InvalidOperationException("Support queue was not found or is inactive.");
        }
    }

    private static TicketDto Map(Ticket ticket) =>
        new(
            ticket.Id,
            ticket.TicketNumber,
            ticket.Type.ToString(),
            ticket.Title,
            ticket.Description,
            ticket.Status.ToString(),
            ticket.Priority.ToString(),
            ticket.RequesterUserId,
            ticket.AssignedUserId,
            ticket.QueueId,
            ticket.ConfigurationItemId,
            ticket.Category,
            ticket.SlaPolicyId,
            ticket.ResponseDueAtUtc,
            ticket.ResolutionDueAtUtc,
            ticket.RespondedAtUtc,
            ticket.ResponseBreached,
            ticket.ResolutionBreached,
            ticket.CreatedAtUtc,
            ticket.UpdatedAtUtc,
            ticket.ResolvedAtUtc,
            ticket.ClosedAtUtc,
            Convert.ToBase64String(ticket.RowVersion));
}
