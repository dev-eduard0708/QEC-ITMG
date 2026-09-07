using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Qec.Itmg.Cmdb.Discovery;
using Qec.Itmg.Cmdb.Services;

namespace Qec.Itmg.Host.Cmdb;

public sealed class NetworkDiscoveryBackgroundService(
    INetworkDiscoveryRunQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<NetworkDiscoveryBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (NetworkDiscoveryRunWorkItem item in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                NetworkDiscoveryService service = scope.ServiceProvider.GetRequiredService<NetworkDiscoveryService>();
                await service.ExecuteRunAsync(item.RunId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Network discovery run {RunId} failed unexpectedly.", item.RunId);
            }
        }
    }
}
