using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.Contracts.Modules;
using Qec.Itmg.ThirdParty.Persistence;
using Qec.Itmg.ThirdParty.Services;

namespace Qec.Itmg.ThirdParty;

public sealed class ThirdPartyModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString(QecEfConventions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{QecEfConventions.ConnectionStringName}' is not configured.");
        }

        services.AddQecSqlServerDbContext<ThirdPartyDbContext>(connectionString, ThirdPartyDbContext.SchemaName);
        services.AddScoped<VendorService>();
    }
}
