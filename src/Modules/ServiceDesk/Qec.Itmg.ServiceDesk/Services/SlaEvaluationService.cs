using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.ServiceDesk.Domain;
using Qec.Itmg.ServiceDesk.Persistence;

namespace Qec.Itmg.ServiceDesk.Services;

public sealed class SlaEvaluationService(ServiceDeskDbContext db, IClock clock)
{
    public async Task<int> MarkBreachesAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset utcNow = clock.UtcNow;
        List<Ticket> candidates = await db.Tickets
            .Where(item =>
                item.Status != TicketStatus.Closed
                && item.Status != TicketStatus.Cancelled
                && (
                    (!item.ResponseBreached
                        && item.RespondedAtUtc == null
                        && item.ResponseDueAtUtc != null
                        && item.ResponseDueAtUtc < utcNow)
                    || (!item.ResolutionBreached
                        && item.Status != TicketStatus.Resolved
                        && item.ResolutionDueAtUtc != null
                        && item.ResolutionDueAtUtc < utcNow)))
            .Take(200)
            .ToListAsync(cancellationToken);

        int marked = 0;
        foreach (Ticket ticket in candidates)
        {
            bool beforeResponse = ticket.ResponseBreached;
            bool beforeResolution = ticket.ResolutionBreached;
            ticket.MarkResponseBreached(utcNow);
            ticket.MarkResolutionBreached(utcNow);
            if (ticket.ResponseBreached != beforeResponse || ticket.ResolutionBreached != beforeResolution)
            {
                marked++;
            }
        }

        if (marked > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return marked;
    }
}
