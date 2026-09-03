using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qec.Itmg.ServiceDesk.Seed;

namespace Qec.Itmg.Host.ServiceDesk;

public static class ServiceDeskSeedHostExtensions
{
    public static IServiceCollection AddServiceDeskSeed(this IServiceCollection services)
    {
        services.AddScoped<IServiceDeskSeedRunner, ServiceDeskSeedRunner>();
        return services;
    }

    public static async Task RunServiceDeskSeedAsync(
        this WebApplication app,
        CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = app.Services.CreateScope();
        IServiceDeskSeedRunner runner = scope.ServiceProvider.GetRequiredService<IServiceDeskSeedRunner>();
        ILogger logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("ServiceDeskSeed");

        try
        {
            await runner.RunAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Service desk seed failed.");
            throw;
        }
    }
}
