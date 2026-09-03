using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.Cmdb.Persistence;
using Qec.Itmg.Cmdb.Services;
using Qec.Itmg.Contracts.Modules;

namespace Qec.Itmg.Cmdb;

public sealed class CmdbModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString(QecEfConventions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{QecEfConventions.ConnectionStringName}' is not configured.");
        }

        services.AddQecSqlServerDbContext<CmdbDbContext>(
            connectionString,
            CmdbDbContext.SchemaName);
        services.AddScoped<ConfigurationItemService>();
        services.AddScoped<CiRelationshipService>();
    }
}
