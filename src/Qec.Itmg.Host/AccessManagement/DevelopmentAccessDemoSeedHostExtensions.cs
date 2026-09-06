using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Qec.Itmg.Host.AccessManagement;

namespace Qec.Itmg.Host.AccessManagement;

public static class DevelopmentAccessDemoSeedHostExtensions
{
    public static IServiceCollection AddDevelopmentAccessDemoSeed(this IServiceCollection services)
    {
        services.AddScoped<IDevelopmentAccessDemoSeedRunner, DevelopmentAccessDemoSeedRunner>();
        return services;
    }

    public static async Task RunDevelopmentAccessDemoSeedAsync(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment()) return;
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        IDevelopmentAccessDemoSeedRunner runner =
            scope.ServiceProvider.GetRequiredService<IDevelopmentAccessDemoSeedRunner>();
        await runner.RunAsync();
    }
}
