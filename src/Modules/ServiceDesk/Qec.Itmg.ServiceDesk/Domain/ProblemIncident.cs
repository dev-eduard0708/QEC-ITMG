namespace Qec.Itmg.ServiceDesk.Domain;

/// <summary>
/// Many-to-many link between a Problem and an Incident ticket.
/// Removing a link must not delete the Problem or Ticket.
/// </summary>
public sealed class ProblemIncident
{
    private ProblemIncident()
    {
    }

    public Guid ProblemId { get; private set; }

    public Guid IncidentTicketId { get; private set; }

    public DateTimeOffset LinkedAtUtc { get; private set; }

    public Guid LinkedByUserId { get; private set; }

    public static ProblemIncident Create(
        Guid problemId,
        Guid incidentTicketId,
        Guid linkedByUserId,
        DateTimeOffset utcNow)
    {
        if (problemId == Guid.Empty)
        {
            throw new ArgumentException("Problem id is required.", nameof(problemId));
        }

        if (incidentTicketId == Guid.Empty)
        {
            throw new ArgumentException("Incident ticket id is required.", nameof(incidentTicketId));
        }

        if (linkedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Linked-by user is required.", nameof(linkedByUserId));
        }

        return new ProblemIncident
        {
            ProblemId = problemId,
            IncidentTicketId = incidentTicketId,
            LinkedByUserId = linkedByUserId,
            LinkedAtUtc = utcNow,
        };
    }
}
