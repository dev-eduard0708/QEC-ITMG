using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Qec.Itmg.Host.Compliance;

public static class ComplianceReadinessSeedHostExtensions
{
    public static IServiceCollection AddComplianceReadinessSeed(this IServiceCollection services)
    {
        services.AddScoped<IComplianceReadinessSeedRunner, ComplianceReadinessSeedRunner>();
        return services;
    }

    public static async Task RunComplianceReadinessSeedAsync(
        this WebApplication app, CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = app.Services.CreateScope();
        IComplianceReadinessSeedRunner runner =
            scope.ServiceProvider.GetRequiredService<IComplianceReadinessSeedRunner>();
        ILogger logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("ComplianceReadinessSeed");

        try
        {
            await runner.RunAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Compliance readiness seed failed.");
            throw;
        }
    }
}
