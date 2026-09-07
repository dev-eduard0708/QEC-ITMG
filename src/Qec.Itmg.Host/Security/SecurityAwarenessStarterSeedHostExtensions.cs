using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qec.Itmg.Security.Services;

namespace Qec.Itmg.Host.Security;

public static class SecurityAwarenessStarterSeedHostExtensions
{
    public static IServiceCollection AddSecurityAwarenessStarterSeed(this IServiceCollection services)
    {
        services.AddScoped<SecurityAwarenessStarterSeedService>();
        return services;
    }

    public static async Task RunSecurityAwarenessStarterSeedAsync(
        this WebApplication app,
        CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = app.Services.CreateScope();
        SecurityAwarenessStarterSeedService seed =
            scope.ServiceProvider.GetRequiredService<SecurityAwarenessStarterSeedService>();
        ILogger logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("SecurityAwarenessStarterSeed");

        try
        {
            await seed.EnsureStarterCampaignsAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Security Awareness starter campaign seed failed.");
            throw;
        }
    }
}
