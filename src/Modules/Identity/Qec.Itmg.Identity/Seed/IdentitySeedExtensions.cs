using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Qec.Itmg.Identity.Seed;

public static class IdentitySeedExtensions
{
    public static IServiceCollection AddIdentitySeed(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<IdentitySeedOptions>(configuration.GetSection(IdentitySeedOptions.SectionName));
        services.AddScoped<IIdentitySeedRunner, IdentitySeedRunner>();
        return services;
    }

    public static async Task RunIdentitySeedAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = app.Services.CreateScope();
        IIdentitySeedRunner runner = scope.ServiceProvider.GetRequiredService<IIdentitySeedRunner>();
        ILogger logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeed");

        try
        {
            await runner.RunAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Identity seed failed.");
            throw;
        }
    }
}
