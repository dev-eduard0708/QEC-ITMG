using Qec.Itmg.ServiceDesk.Services;

namespace Qec.Itmg.Host.ServiceDesk;

public sealed class SlaBreachDetectionJob(SlaEvaluationService evaluation)
{
    public Task<int> ExecuteAsync(CancellationToken cancellationToken = default) =>
        evaluation.MarkBreachesAsync(cancellationToken);
}
