using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Qec.Itmg.Host.Organization;

public static class OrganizationPositionsSeedHostExtensions
{
    public static IServiceCollection AddOrganizationPositionsSeed(this IServiceCollection services)
    {
        services.AddScoped<IOrganizationPositionsSeedRunner, OrganizationPositionsSeedRunner>();
        return services;
    }

    public static async Task RunOrganizationPositionsSeedAsync(
        this WebApplication app,
        CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = app.Services.CreateScope();
        IOrganizationPositionsSeedRunner runner =
            scope.ServiceProvider.GetRequiredService<IOrganizationPositionsSeedRunner>();
        ILogger logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("OrganizationPositionsSeed");

        try
        {
            await runner.RunAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Organization positions seed failed.");
            throw;
        }
    }
}
