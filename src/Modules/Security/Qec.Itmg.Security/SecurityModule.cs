using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.Contracts.Modules;
using Qec.Itmg.Security.Persistence;
using Qec.Itmg.Security.Services;

namespace Qec.Itmg.Security;

public sealed class SecurityModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString(QecEfConventions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{QecEfConventions.ConnectionStringName}' is not configured.");
        }

        services.AddQecSqlServerDbContext<SecurityDbContext>(connectionString, SecurityDbContext.SchemaName);
        services.AddScoped<SecurityService>();
        // IVulnerabilityScannerIngestClient registered by PlatformModule (real adapter; disabled by default).
    }
}
