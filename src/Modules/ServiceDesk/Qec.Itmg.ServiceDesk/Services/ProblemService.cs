using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Contracts.Numbering;
using Qec.Itmg.ServiceDesk.Domain;
using Qec.Itmg.ServiceDesk.Persistence;

namespace Qec.Itmg.ServiceDesk.Services;

public sealed record ProblemDto(
    Guid Id,
    string ProblemNumber,
    string Title,
    string Description,
    string Status,
    string Priority,
    Guid? OwnerUserId,
    Guid? ConfigurationItemId,
    string? RootCause,
    string? Workaround,
    bool IsKnownError,
    DateTimeOffset? KnownErrorAtUtc,
    Guid? KnownErrorByUserId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ResolvedAtUtc,
    DateTimeOffset? ClosedAtUtc,
    string RowVersion);

public sealed record ProblemListResult(
    IReadOnlyList<ProblemDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record ProblemRecurringMetricsDto(
    int LinkedIncidentCount,
    int OpenLinkedIncidents,
    int MajorLinkedIncidents,
    DateTimeOffset? FirstOccurrenceUtc,
    DateTimeOffset? LatestOccurrenceUtc,
    int RecentOccurrenceCount,
    int RecentWindowDays);

public sealed record RecurringIncidentGroupDto(
    string GroupType,
    string GroupKey,
    int IncidentCount,
    int LinkedProblemCount);

public sealed record ProblemIncidentDto(
    Guid ProblemId,
    Guid IncidentTicketId,
    string TicketNumber,
    string Title,
    string Status,
    string Priority,
    bool IsMajorIncident,
    DateTimeOffset LinkedAtUtc,
    Guid LinkedByUserId);

public sealed record RelatedProblemDto(
    Guid ProblemId,
    string ProblemNumber,
    string Title,
    string Status,
    DateTimeOffset LinkedAtUtc);

public sealed class ProblemService(
    ServiceDeskDbContext db,
    INumberSequenceService numbers,
    IClock clock,
    IBusinessAuditWriter businessAudit,
    ISharedDbTransaction sharedDbTransaction)
{
    public const string SequenceKey = "problems";
    public const string Prefix = "PRB";

    public async Task<ProblemListResult> ListAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        ProblemStatus? status = null,
        TicketPriority? priority = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        IQueryable<Problem> query = db.Problems.AsNoTracking();
        if (status is ProblemStatus statusFilter)
        {
            query = query.Where(item => item.Status == statusFilter);
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
                || item.ProblemNumber.Contains(term)
                || item.Description.Contains(term));
        }

        int total = await query.CountAsync(cancellationToken);
        List<Problem> items = await query
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new ProblemListResult(items.Select(Map).ToList(), total, page, pageSize);
    }

    public async Task<ProblemDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Problem? problem = await db.Problems.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return problem is null ? null : Map(problem);
    }

    public async Task<Problem> CreateAsync(
        string title,
        string description,
        TicketPriority priority = TicketPriority.Medium,
        Guid? ownerUserId = null,
        Guid? configurationItemId = null,
        CancellationToken cancellationToken = default)
    {
        string problemNumber = await numbers.NextAsync(SequenceKey, Prefix, cancellationToken);
        Problem problem = Problem.Create(
            problemNumber,
            title,
            description,
            priority,
            clock.UtcNow,
            ownerUserId,
            configurationItemId);

        await sharedDbTransaction.ExecuteAsync(
            async ct =>
            {
                db.Problems.Add(problem);
                await businessAudit.AppendAsync(
                    ServiceDeskAuditComposer.ProblemCreated(problem.Id, problem.ProblemNumber),
                    ct);
                await db.SaveChangesAsync(ct);
            },
            cancellationToken);

        return problem;
    }

    public async Task<Problem> UpdateAsync(
        Guid id,
        string title,
        string description,
        TicketPriority priority,
        Guid? ownerUserId,
        Guid? configurationItemId,
        string? rootCause,
        string? workaround,
        string rowVersion,
        CancellationToken cancellationToken = default)
    {
        Problem problem = await db.Problems.FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Problem was not found.");

        string beforeTitle = problem.Title;
        string beforeDescription = problem.Description;
        TicketPriority beforePriority = problem.Priority;
        Guid? beforeOwner = problem.OwnerUserId;
        Guid? beforeCi = problem.ConfigurationItemId;
        string? beforeRoot = problem.RootCause;
        string? beforeWorkaround = problem.Workaround;

        problem.UpdateDetails(
            title,
            description,
            priority,
            ownerUserId,
            configurationItemId,
            rootCause,
            workaround,
            rowVersion,
            clock.UtcNow);

        List<BusinessAuditEntry> audits = [];
        AddIfChanged(audits, problem, "Title", beforeTitle, problem.Title);
        AddIfChanged(audits, problem, "Description", beforeDescription, problem.Description);
        AddIfChanged(audits, problem, "Priority", beforePriority.ToString(), problem.Priority.ToString());
        AddIfChanged(audits, problem, "OwnerUserId", beforeOwner?.ToString("D"), problem.OwnerUserId?.ToString("D"));
        AddIfChanged(
            audits,
            problem,
            "ConfigurationItemId",
            beforeCi?.ToString("D"),
            problem.ConfigurationItemId?.ToString("D"));
        AddIfChanged(audits, problem, "RootCause", beforeRoot, problem.RootCause);
        AddIfChanged(audits, problem, "Workaround", beforeWorkaround, problem.Workaround);

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

        return problem;
    }

    public async Task<Problem> ChangeStatusAsync(
        Guid id,
        ProblemStatus status,
        string? rowVersion = null,
        CancellationToken cancellationToken = default)
    {
        Problem problem = await db.Problems.FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Problem was not found.");

        ProblemStatus from = problem.Status;
        problem.ChangeStatus(status, clock.UtcNow, rowVersion);

        await sharedDbTransaction.ExecuteAsync(
            async ct =>
            {
                if (from != problem.Status)
                {
                    await businessAudit.AppendAsync(
                        ServiceDeskAuditComposer.ProblemField(
                            problem.Id,
                            problem.ProblemNumber,
                            "Status",
                            from.ToString(),
                            problem.Status.ToString(),
                            BusinessAuditAction.StatusChanged),
                        ct);
                }

                await db.SaveChangesAsync(ct);
            },
            cancellationToken);

        return problem;
    }

    public async Task<Problem> SetKnownErrorAsync(
        Guid id,
        bool isKnownError,
        Guid byUserId,
        string rowVersion,
        CancellationToken cancellationToken = default)
    {
        Problem problem = await db.Problems.FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Problem was not found.");

        bool previous = problem.IsKnownError;
        problem.SetKnownError(isKnownError, byUserId, rowVersion, clock.UtcNow);

        await sharedDbTransaction.ExecuteAsync(
            async ct =>
            {
                if (previous != problem.IsKnownError)
                {
                    await businessAudit.AppendAsync(
                        ServiceDeskAuditComposer.ProblemField(
                            problem.Id,
                            problem.ProblemNumber,
                            "IsKnownError",
                            previous.ToString(),
                            problem.IsKnownError.ToString()),
                        ct);
                }

                await db.SaveChangesAsync(ct);
            },
            cancellationToken);

        return problem;
    }

    public async Task<ProblemRecurringMetricsDto?> GetRecurringMetricsAsync(
        Guid problemId,
        int recentWindowDays = 30,
        CancellationToken cancellationToken = default)
    {
        recentWindowDays = Math.Clamp(recentWindowDays, 1, 365);
        bool exists = await db.Problems.AsNoTracking().AnyAsync(item => item.Id == problemId, cancellationToken);
        if (!exists)
        {
            return null;
        }

        DateTimeOffset recentFrom = clock.UtcNow.AddDays(-recentWindowDays);
        TicketStatus[] closed =
        [
            TicketStatus.Resolved,
            TicketStatus.Closed,
            TicketStatus.Cancelled,
        ];

        var tickets = await (
            from link in db.ProblemIncidents.AsNoTracking()
            join ticket in db.Tickets.AsNoTracking() on link.IncidentTicketId equals ticket.Id
            where link.ProblemId == problemId && ticket.Type == TicketType.Incident
            select ticket).ToListAsync(cancellationToken);

        if (tickets.Count == 0)
        {
            return new ProblemRecurringMetricsDto(0, 0, 0, null, null, 0, recentWindowDays);
        }

        return new ProblemRecurringMetricsDto(
            LinkedIncidentCount: tickets.Count,
            OpenLinkedIncidents: tickets.Count(item => !closed.Contains(item.Status)),
            MajorLinkedIncidents: tickets.Count(item => item.IsMajorIncident),
            FirstOccurrenceUtc: tickets.Min(item => item.CreatedAtUtc),
            LatestOccurrenceUtc: tickets.Max(item => item.CreatedAtUtc),
            RecentOccurrenceCount: tickets.Count(item => item.CreatedAtUtc >= recentFrom),
            RecentWindowDays: recentWindowDays);
    }

    public async Task<IReadOnlyList<RecurringIncidentGroupDto>> ListTopRecurringGroupsAsync(
        int take = 10,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 25);

        var linked = await (
            from link in db.ProblemIncidents.AsNoTracking()
            join ticket in db.Tickets.AsNoTracking() on link.IncidentTicketId equals ticket.Id
            where ticket.Type == TicketType.Incident
            select new { ticket.ConfigurationItemId, ticket.Category, link.ProblemId }).ToListAsync(cancellationToken);

        List<RecurringIncidentGroupDto> groups = [];

        groups.AddRange(
            linked
                .Where(item => item.ConfigurationItemId is not null)
                .GroupBy(item => item.ConfigurationItemId!.Value)
                .Select(group => new RecurringIncidentGroupDto(
                    "ConfigurationItem",
                    group.Key.ToString("D"),
                    group.Select(item => item).Count(),
                    group.Select(item => item.ProblemId).Distinct().Count()))
                .OrderByDescending(item => item.IncidentCount)
                .Take(take));

        groups.AddRange(
            linked
                .Where(item => !string.IsNullOrWhiteSpace(item.Category))
                .GroupBy(item => item.Category!)
                .Select(group => new RecurringIncidentGroupDto(
                    "Category",
                    group.Key,
                    group.Count(),
                    group.Select(item => item.ProblemId).Distinct().Count()))
                .OrderByDescending(item => item.IncidentCount)
                .Take(take));

        return groups
            .OrderByDescending(item => item.IncidentCount)
            .Take(take)
            .ToList();
    }

    public async Task<IReadOnlyList<ProblemIncidentDto>> ListIncidentsAsync(
        Guid problemId,
        CancellationToken cancellationToken = default)
    {
        bool exists = await db.Problems.AsNoTracking().AnyAsync(item => item.Id == problemId, cancellationToken);
        if (!exists)
        {
            throw new InvalidOperationException("Problem was not found.");
        }

        var rows = await (
            from link in db.ProblemIncidents.AsNoTracking()
            join ticket in db.Tickets.AsNoTracking() on link.IncidentTicketId equals ticket.Id
            where link.ProblemId == problemId
            orderby link.LinkedAtUtc descending
            select new { link, ticket }).ToListAsync(cancellationToken);

        return rows
            .Select(row => new ProblemIncidentDto(
                row.link.ProblemId,
                row.link.IncidentTicketId,
                row.ticket.TicketNumber,
                row.ticket.Title,
                row.ticket.Status.ToString(),
                row.ticket.Priority.ToString(),
                row.ticket.IsMajorIncident,
                row.link.LinkedAtUtc,
                row.link.LinkedByUserId))
            .ToList();
    }

    public async Task<IReadOnlyList<RelatedProblemDto>> ListProblemsForIncidentAsync(
        Guid incidentTicketId,
        CancellationToken cancellationToken = default)
    {
        var rows = await (
            from link in db.ProblemIncidents.AsNoTracking()
            join problem in db.Problems.AsNoTracking() on link.ProblemId equals problem.Id
            where link.IncidentTicketId == incidentTicketId
            orderby link.LinkedAtUtc descending
            select new { link, problem }).ToListAsync(cancellationToken);

        return rows
            .Select(row => new RelatedProblemDto(
                row.problem.Id,
                row.problem.ProblemNumber,
                row.problem.Title,
                row.problem.Status.ToString(),
                row.link.LinkedAtUtc))
            .ToList();
    }

    public async Task LinkIncidentAsync(
        Guid problemId,
        Guid incidentTicketId,
        Guid linkedByUserId,
        CancellationToken cancellationToken = default)
    {
        Problem problem = await db.Problems.FirstOrDefaultAsync(item => item.Id == problemId, cancellationToken)
            ?? throw new InvalidOperationException("Problem was not found.");

        Ticket ticket = await db.Tickets.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == incidentTicketId, cancellationToken)
            ?? throw new InvalidOperationException("Ticket was not found.");

        if (ticket.Type != TicketType.Incident)
        {
            throw new InvalidOperationException("Only Incident tickets can be linked to a problem.");
        }

        bool alreadyLinked = await db.ProblemIncidents.AnyAsync(
            item => item.ProblemId == problemId && item.IncidentTicketId == incidentTicketId,
            cancellationToken);
        if (alreadyLinked)
        {
            return;
        }

        ProblemIncident link = ProblemIncident.Create(problemId, incidentTicketId, linkedByUserId, clock.UtcNow);

        await sharedDbTransaction.ExecuteAsync(
            async ct =>
            {
                db.ProblemIncidents.Add(link);
                await businessAudit.AppendAsync(
                    ServiceDeskAuditComposer.ProblemIncidentLinked(
                        problem.Id,
                        problem.ProblemNumber,
                        ticket.Id,
                        ticket.TicketNumber),
                    ct);
                await db.SaveChangesAsync(ct);
            },
            cancellationToken);
    }

    public async Task UnlinkIncidentAsync(
        Guid problemId,
        Guid incidentTicketId,
        CancellationToken cancellationToken = default)
    {
        Problem problem = await db.Problems.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == problemId, cancellationToken)
            ?? throw new InvalidOperationException("Problem was not found.");

        ProblemIncident? link = await db.ProblemIncidents.FirstOrDefaultAsync(
            item => item.ProblemId == problemId && item.IncidentTicketId == incidentTicketId,
            cancellationToken);
        if (link is null)
        {
            return;
        }

        Ticket? ticket = await db.Tickets.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == incidentTicketId, cancellationToken);

        await sharedDbTransaction.ExecuteAsync(
            async ct =>
            {
                db.ProblemIncidents.Remove(link);
                await businessAudit.AppendAsync(
                    ServiceDeskAuditComposer.ProblemIncidentUnlinked(
                        problem.Id,
                        problem.ProblemNumber,
                        incidentTicketId,
                        ticket?.TicketNumber),
                    ct);
                await db.SaveChangesAsync(ct);
            },
            cancellationToken);
    }

    private static void AddIfChanged(
        List<BusinessAuditEntry> audits,
        Problem problem,
        string field,
        string? oldValue,
        string? newValue)
    {
        if (string.Equals(oldValue, newValue, StringComparison.Ordinal))
        {
            return;
        }

        audits.Add(
            ServiceDeskAuditComposer.ProblemField(
                problem.Id,
                problem.ProblemNumber,
                field,
                oldValue,
                newValue));
    }

    private static ProblemDto Map(Problem problem) =>
        new(
            problem.Id,
            problem.ProblemNumber,
            problem.Title,
            problem.Description,
            problem.Status.ToString(),
            problem.Priority.ToString(),
            problem.OwnerUserId,
            problem.ConfigurationItemId,
            problem.RootCause,
            problem.Workaround,
            problem.IsKnownError,
            problem.KnownErrorAtUtc,
            problem.KnownErrorByUserId,
            problem.CreatedAtUtc,
            problem.UpdatedAtUtc,
            problem.ResolvedAtUtc,
            problem.ClosedAtUtc,
            Convert.ToBase64String(problem.RowVersion));
}
