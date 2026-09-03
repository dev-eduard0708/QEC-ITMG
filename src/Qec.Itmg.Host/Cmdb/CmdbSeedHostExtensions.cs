using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qec.Itmg.Cmdb.Seed;

namespace Qec.Itmg.Host.Cmdb;

public static class CmdbSeedHostExtensions
{
    public static IServiceCollection AddCmdbSeed(this IServiceCollection services)
    {
        services.AddScoped<ICmdbSeedRunner, CmdbSeedRunner>();
        return services;
    }

    public static async Task RunCmdbSeedAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = app.Services.CreateScope();
        ICmdbSeedRunner runner = scope.ServiceProvider.GetRequiredService<ICmdbSeedRunner>();
        ILogger logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("CmdbSeed");

        try
        {
            await runner.RunAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "CMDB seed failed.");
            throw;
        }
    }
}
