using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Qec.Itmg.Host.AccessManagement;

public static class AccessCatalogSeedHostExtensions
{
    public static IServiceCollection AddAccessCatalogSeed(this IServiceCollection services)
    {
        services.AddScoped<IAccessCatalogSeedRunner, AccessCatalogSeedRunner>();
        return services;
    }

    public static async Task RunAccessCatalogSeedAsync(
        this WebApplication app, CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = app.Services.CreateScope();
        IAccessCatalogSeedRunner runner =
            scope.ServiceProvider.GetRequiredService<IAccessCatalogSeedRunner>();
        ILogger logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("AccessCatalogSeed");

        try
        {
            await runner.RunAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Access catalog seed failed.");
            throw;
        }
    }
}
