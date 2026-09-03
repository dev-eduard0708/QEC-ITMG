using Qec.Itmg.ServiceDesk.Services;

namespace Qec.Itmg.Host.ServiceDesk;

public sealed class SlaBreachDetectionJob(
    SlaEvaluationService evaluation,
    TicketNotificationService notifications)
{
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SlaBreachEvent> breaches = await evaluation.MarkBreachesAsync(cancellationToken);
        if (breaches.Count > 0)
        {
            await notifications.NotifySlaBreachesAsync(breaches, cancellationToken);
        }

        return breaches.Count;
    }
}
