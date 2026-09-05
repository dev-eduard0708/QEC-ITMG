using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.BusinessContinuity.Persistence;
using Qec.Itmg.BusinessContinuity.Services;
using Qec.Itmg.Contracts.Continuity;
using Qec.Itmg.Contracts.Modules;

namespace Qec.Itmg.BusinessContinuity;

public sealed class ContinuityModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString(QecEfConventions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{QecEfConventions.ConnectionStringName}' is not configured.");
        }

        services.AddQecSqlServerDbContext<ContinuityDbContext>(connectionString, ContinuityDbContext.SchemaName);
        services.AddScoped<ContinuityService>();
        services.AddScoped<IDrTestCoverageQuery>(sp => sp.GetRequiredService<ContinuityService>());
    }
}
