using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Audit;
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
    string RowVersion,
    bool IsMajorIncident,
    string? SecurityClassification,
    Guid? SourceEventId);

public sealed record SupportQueueDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive);

public sealed record TicketAssignmentHistoryDto(
    Guid Id,
    Guid TicketId,
    Guid? QueueId,
    Guid? AssignedUserId,
    Guid AssignedByUserId,
    DateTimeOffset AssignedAtUtc,
    string? Notes);

public sealed record TicketStatusHistoryDto(
    Guid Id,
    Guid TicketId,
    string FromStatus,
    string ToStatus,
    Guid ChangedByUserId,
    DateTimeOffset ChangedAtUtc);

public sealed record SlaBreachEvent(
    Guid TicketId,
    string TicketNumber,
    Guid? AssignedUserId,
    Guid RequesterUserId,
    bool ResponseNewlyBreached,
    bool ResolutionNewlyBreached);

public sealed record TicketListResult(
    IReadOnlyList<TicketDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record TicketDashboardDto(
    int OpenTickets,
    int Unassigned,
    int CriticalOpen,
    int SlaBreached,
    int MyAssigned,
    int NewToday,
    int ResolvedToday,
    IReadOnlyDictionary<string, int> ByStatus,
    IReadOnlyDictionary<string, int> ByPriority);

public sealed record ServiceDeskReportDto(
    DateTimeOffset GeneratedAtUtc,
    int OpenTickets,
    int Backlog,
    int SlaBreachedOpen,
    int ResponseBreachedOpen,
    int ResolutionBreachedOpen,
    IReadOnlyDictionary<string, int> OpenByPriority,
    IReadOnlyDictionary<string, int> OpenByStatus,
    IReadOnlyDictionary<string, int> WorkloadByAssignee,
    double? MedianFirstResponseMinutes,
    double? MedianResolutionMinutes,
    string Note);

public sealed record IncidentReportDto(
    DateTimeOffset GeneratedAtUtc,
    int OpenIncidents,
    int MajorIncidentsOpen,
    int MajorIncidentsPeriod,
    double? MedianMttaMinutes,
    double? MedianMttrMinutes,
    int CreatedInPeriod,
    int ResolvedInPeriod,
    string Note);

public sealed class TicketService(
    ServiceDeskDbContext db,
    INumberSequenceService numbers,
    IClock clock,
    IBusinessAuditWriter businessAudit,
    ISharedDbTransaction sharedDbTransaction)
{
    public const string IncidentSequenceKey = "tickets-incident";
    public const string ServiceRequestSequenceKey = "tickets-service-request";
    public const string IncidentPrefix = "INC";
    public const string ServiceRequestPrefix = "SR";
    public const string IncidentsSecurityPermission = "incidents.security";

    private static readonly TicketStatus[] ActiveStatuses =
    [
        TicketStatus.New,
        TicketStatus.Open,
        TicketStatus.InProgress,
        TicketStatus.PendingRequester,
    ];

    public async Task<TicketDashboardDto> GetDashboardAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset utcNow = clock.UtcNow;
        DateTimeOffset dayStart = new(utcNow.UtcDateTime.Date, TimeSpan.Zero);

        List<Ticket> tickets = await db.Tickets.AsNoTracking().ToListAsync(cancellationToken);
        List<Ticket> open = tickets.Where(item => ActiveStatuses.Contains(item.Status)).ToList();

        Dictionary<string, int> byStatus = tickets
            .GroupBy(item => item.Status.ToString())
            .ToDictionary(group => group.Key, group => group.Count());

        Dictionary<string, int> byPriority = open
            .GroupBy(item => item.Priority.ToString())
            .ToDictionary(group => group.Key, group => group.Count());

        return new TicketDashboardDto(
            OpenTickets: open.Count,
            Unassigned: open.Count(item => item.AssignedUserId is null),
            CriticalOpen: open.Count(item => item.Priority == TicketPriority.Critical),
            SlaBreached: open.Count(item => item.ResponseBreached || item.ResolutionBreached),
            MyAssigned: open.Count(item => item.AssignedUserId == currentUserId),
            NewToday: tickets.Count(item => item.CreatedAtUtc >= dayStart),
            ResolvedToday: tickets.Count(item =>
                item.ResolvedAtUtc is DateTimeOffset resolved && resolved >= dayStart),
            ByStatus: byStatus,
            ByPriority: byPriority);
    }

    public async Task<int> CountOpenSecurityIncidentsAsync(CancellationToken cancellationToken = default)
    {
        return await db.Tickets.AsNoTracking().CountAsync(
            item => item.Type == TicketType.Incident
                && ActiveStatuses.Contains(item.Status)
                && item.SecurityClassification != SecurityClassification.None,
            cancellationToken);
    }

    public async Task<ServiceDeskReportDto> GetServiceDeskReportAsync(
        DateTimeOffset? periodStartUtc, DateTimeOffset? periodEndUtc, CancellationToken ct = default)
    {
        DateTimeOffset now = clock.UtcNow;
        List<Ticket> tickets = await db.Tickets.AsNoTracking().ToListAsync(ct);
        List<Ticket> open = tickets.Where(x => ActiveStatuses.Contains(x.Status)).ToList();
        IEnumerable<Ticket> period = tickets.Where(x =>
            (!periodStartUtc.HasValue || x.CreatedAtUtc >= periodStartUtc) &&
            (!periodEndUtc.HasValue || x.CreatedAtUtc <= periodEndUtc));

        List<double> frt = tickets
            .Where(x => x.RespondedAtUtc is not null)
            .Select(x => (x.RespondedAtUtc!.Value - x.CreatedAtUtc).TotalMinutes)
            .Where(m => m >= 0)
            .OrderBy(m => m)
            .ToList();
        List<double> res = tickets
            .Where(x => x.ResolvedAtUtc is not null)
            .Select(x => (x.ResolvedAtUtc!.Value - x.CreatedAtUtc).TotalMinutes)
            .Where(m => m >= 0)
            .OrderBy(m => m)
            .ToList();

        return new ServiceDeskReportDto(
            now,
            open.Count,
            open.Count,
            open.Count(x => x.ResponseBreached || x.ResolutionBreached),
            open.Count(x => x.ResponseBreached),
            open.Count(x => x.ResolutionBreached),
            open.GroupBy(x => x.Priority.ToString()).ToDictionary(g => g.Key, g => g.Count()),
            open.GroupBy(x => x.Status.ToString()).ToDictionary(g => g.Key, g => g.Count()),
            open.Where(x => x.AssignedUserId is not null)
                .GroupBy(x => x.AssignedUserId!.Value.ToString())
                .ToDictionary(g => g.Key, g => g.Count()),
            Median(frt),
            Median(res),
            "Live aggregates. Median FRT/resolution only where RespondedAt/ResolvedAt exist.");
    }

    public async Task<IncidentReportDto> GetIncidentReportAsync(
        DateTimeOffset? periodStartUtc, DateTimeOffset? periodEndUtc, CancellationToken ct = default)
    {
        DateTimeOffset now = clock.UtcNow;
        DateTimeOffset start = periodStartUtc ?? now.AddDays(-30);
        DateTimeOffset end = periodEndUtc ?? now;
        List<Ticket> incidents = await db.Tickets.AsNoTracking()
            .Where(x => x.Type == TicketType.Incident)
            .ToListAsync(ct);
        List<Ticket> open = incidents.Where(x => ActiveStatuses.Contains(x.Status)).ToList();
        List<Ticket> periodCreated = incidents.Where(x => x.CreatedAtUtc >= start && x.CreatedAtUtc <= end).ToList();
        List<Ticket> periodResolved = incidents
            .Where(x => x.ResolvedAtUtc is DateTimeOffset r && r >= start && r <= end)
            .ToList();

        List<double> mtta = incidents
            .Where(x => x.RespondedAtUtc is not null)
            .Select(x => (x.RespondedAtUtc!.Value - x.CreatedAtUtc).TotalMinutes)
            .Where(m => m >= 0).OrderBy(m => m).ToList();
        List<double> mttr = incidents
            .Where(x => x.ResolvedAtUtc is not null)
            .Select(x => (x.ResolvedAtUtc!.Value - x.CreatedAtUtc).TotalMinutes)
            .Where(m => m >= 0).OrderBy(m => m).ToList();

        return new IncidentReportDto(
            now,
            open.Count,
            open.Count(x => x.IsMajorIncident),
            periodCreated.Count(x => x.IsMajorIncident),
            Median(mtta),
            Median(mttr),
            periodCreated.Count,
            periodResolved.Count,
            "MTTA/MTTR are median minutes only when RespondedAt/ResolvedAt timestamps exist; otherwise null.");
    }

    private static double? Median(IReadOnlyList<double> ordered)
    {
        if (ordered.Count == 0) return null;
        int mid = ordered.Count / 2;
        return ordered.Count % 2 == 0
            ? (ordered[mid - 1] + ordered[mid]) / 2.0
            : ordered[mid];
    }

    public async Task<TicketListResult> ListAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        TicketStatus? status = null,
        TicketType? type = null,
        TicketPriority? priority = null,
        Guid? requesterUserId = null,
        bool includeSecurityClassification = false,
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

        return new TicketListResult(
            items.Select(item => Map(item, includeSecurityClassification)).ToList(),
            total,
            page,
            pageSize);
    }

    public async Task<TicketDto?> GetAsync(
        Guid id,
        bool includeSecurityClassification = false,
        CancellationToken cancellationToken = default)
    {
        Ticket? ticket = await db.Tickets.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return ticket is null ? null : Map(ticket, includeSecurityClassification);
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
        // Employee self-service must never receive security classification.
        return ticket is null ? null : Map(ticket, includeSecurityClassification: false);
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

    /// <summary>
    /// P5 stub only: promote a future Event into an Incident ticket.
    /// P8 will replace/extend this with the real Event aggregate and FK/relationship.
    /// </summary>
    public async Task<Ticket> PromoteFromEventAsync(
        Guid eventId,
        string title,
        string description,
        Guid requesterUserId,
        TicketPriority priority = TicketPriority.Medium,
        Guid? configurationItemId = null,
        CancellationToken cancellationToken = default)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException("Event id is required.", nameof(eventId));
        }

        Ticket? existing = await db.Tickets
            .FirstOrDefaultAsync(
                item => item.SourceEventId == eventId && item.Type == TicketType.Incident,
                cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        Ticket ticket = await CreateAsync(
            TicketType.Incident,
            title,
            description,
            requesterUserId,
            priority,
            configurationItemId,
            cancellationToken: cancellationToken);

        Ticket tracked = await db.Tickets.FirstAsync(item => item.Id == ticket.Id, cancellationToken);
        tracked.BindSourceEvent(eventId, clock.UtcNow);

        await sharedDbTransaction.ExecuteAsync(
            async ct =>
            {
                await businessAudit.AppendAsync(
                    ServiceDeskAuditComposer.TicketField(
                        tracked.Id,
                        tracked.TicketNumber,
                        "SourceEventId",
                        null,
                        eventId.ToString("D"),
                        BusinessAuditAction.Linked),
                    ct);
                await db.SaveChangesAsync(ct);
            },
            cancellationToken);

        return tracked;
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

    public async Task<Ticket> UpdateIncidentAsync(
        Guid id,
        bool isMajorIncident,
        SecurityClassification? securityClassification,
        bool updateSecurityClassification,
        string rowVersion,
        CancellationToken cancellationToken = default)
    {
        Ticket ticket = await db.Tickets.FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Ticket was not found.");

        if (ticket.Type != TicketType.Incident)
        {
            throw new InvalidOperationException("Incident specialization applies only to Incident tickets.");
        }

        bool previousMajor = ticket.IsMajorIncident;
        SecurityClassification previousClassification = ticket.SecurityClassification;

        ticket.UpdateIncidentSpecialization(
            isMajorIncident,
            securityClassification,
            updateSecurityClassification,
            rowVersion,
            clock.UtcNow);

        List<BusinessAuditEntry> audits = [];
        if (previousMajor != ticket.IsMajorIncident)
        {
            audits.Add(
                ServiceDeskAuditComposer.TicketField(
                    ticket.Id,
                    ticket.TicketNumber,
                    "IsMajorIncident",
                    previousMajor.ToString(),
                    ticket.IsMajorIncident.ToString()));
        }

        if (updateSecurityClassification && previousClassification != ticket.SecurityClassification)
        {
            audits.Add(
                ServiceDeskAuditComposer.TicketField(
                    ticket.Id,
                    ticket.TicketNumber,
                    "SecurityClassification",
                    previousClassification.ToString(),
                    ticket.SecurityClassification.ToString()));
        }

        await sharedDbTransaction.ExecuteAsync(
            async ct =>
            {
                if (audits.Count > 0)
                {
                    await businessAudit.AppendManyAsync(audits, ct);
                }

                await db.SaveChangesAsync(ct);
            },
            cancellationToken);

        return ticket;
    }

    public async Task<Ticket> ChangeStatusAsync(
        Guid id,
        TicketStatus status,
        Guid changedByUserId,
        string? rowVersion = null,
        CancellationToken cancellationToken = default)
    {
        Ticket ticket = await db.Tickets.FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Ticket was not found.");

        TicketStatus from = ticket.Status;
        ticket.ChangeStatus(status, clock.UtcNow, rowVersion);
        if (from != ticket.Status)
        {
            db.TicketStatusHistories.Add(
                TicketStatusHistory.Create(ticket.Id, from, ticket.Status, changedByUserId, clock.UtcNow));
        }

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

        TicketStatus beforeStatus = ticket.Status;
        ticket.Assign(queueId, assignedUserId, clock.UtcNow);

        db.TicketAssignmentHistories.Add(
            TicketAssignmentHistory.Create(
                ticket.Id,
                assignedByUserId,
                clock.UtcNow,
                ticket.QueueId,
                ticket.AssignedUserId,
                notes));

        if (beforeStatus != ticket.Status)
        {
            db.TicketStatusHistories.Add(
                TicketStatusHistory.Create(
                    ticket.Id,
                    beforeStatus,
                    ticket.Status,
                    assignedByUserId,
                    clock.UtcNow));
        }

        await db.SaveChangesAsync(cancellationToken);
        return ticket;
    }

    public async Task<IReadOnlyList<TicketAssignmentHistoryDto>> ListAssignmentHistoryAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default)
    {
        return await db.TicketAssignmentHistories.AsNoTracking()
            .Where(item => item.TicketId == ticketId)
            .OrderBy(item => item.AssignedAtUtc)
            .ThenBy(item => item.Id)
            .Select(item => new TicketAssignmentHistoryDto(
                item.Id,
                item.TicketId,
                item.QueueId,
                item.AssignedUserId,
                item.AssignedByUserId,
                item.AssignedAtUtc,
                item.Notes))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TicketStatusHistoryDto>> ListStatusHistoryAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default)
    {
        return await db.TicketStatusHistories.AsNoTracking()
            .Where(item => item.TicketId == ticketId)
            .OrderBy(item => item.ChangedAtUtc)
            .ThenBy(item => item.Id)
            .Select(item => new TicketStatusHistoryDto(
                item.Id,
                item.TicketId,
                item.FromStatus.ToString(),
                item.ToStatus.ToString(),
                item.ChangedByUserId,
                item.ChangedAtUtc))
            .ToListAsync(cancellationToken);
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

    private static TicketDto Map(Ticket ticket, bool includeSecurityClassification) =>
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
            Convert.ToBase64String(ticket.RowVersion),
            ticket.Type == TicketType.Incident && ticket.IsMajorIncident,
            includeSecurityClassification && ticket.Type == TicketType.Incident
                ? ticket.SecurityClassification.ToString()
                : null,
            ticket.Type == TicketType.Incident ? ticket.SourceEventId : null);
}
