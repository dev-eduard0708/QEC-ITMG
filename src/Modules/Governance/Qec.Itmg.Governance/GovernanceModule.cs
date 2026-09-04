using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.Contracts.Modules;
using Qec.Itmg.Governance.Persistence;
using Qec.Itmg.Governance.Services;

namespace Qec.Itmg.Governance;

public sealed class GovernanceModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString(QecEfConventions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{QecEfConventions.ConnectionStringName}' is not configured.");
        }

        services.AddQecSqlServerDbContext<GovernanceDbContext>(
            connectionString,
            GovernanceDbContext.SchemaName);
        services.AddScoped<OrganizationChartService>();
        services.AddScoped<InternalControlService>();
    }
}
