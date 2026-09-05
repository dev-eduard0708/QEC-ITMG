using Hangfire;
using Microsoft.Extensions.Logging;
using Qec.Itmg.Platform.Integrations;

namespace Qec.Itmg.Host.Integrations;

public sealed class IntegrationPollingJob(
    IntegrationSyncCoordinator sync,
    DirectoryJmlFulfillmentService jml,
    ILogger<IntegrationPollingJob> logger)
{
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 55)]
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<IntegrationSyncResult> results = await sync.SyncEnabledAsync(cancellationToken);
        foreach (IntegrationSyncResult result in results)
        {
            logger.LogInformation(
                "Integration sync {Provider} status={Status} processed={Processed} unmatched={Unmatched} corr={Correlation}",
                result.Provider, result.Status, result.Processed, result.Unmatched, result.CorrelationId);
        }

        int jmlCount = await jml.ExecuteEligibleAsync(cancellationToken);
        if (jmlCount > 0)
            logger.LogInformation("Directory JML fulfillment executed {Count} checklist actions", jmlCount);
    }
}
