using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qec.Itmg.Audit.Persistence;
using Qec.Itmg.Audit.Services;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.Contracts.Modules;

namespace Qec.Itmg.Audit;

public sealed class AuditModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString(QecEfConventions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{QecEfConventions.ConnectionStringName}' is not configured.");
        }

        services.AddQecSqlServerDbContext<AuditDbContext>(connectionString, AuditDbContext.SchemaName);
        services.AddScoped<AuditService>();
    }
}
