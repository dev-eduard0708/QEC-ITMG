using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.Contracts.Evidence;
using Qec.Itmg.Contracts.Modules;
using Qec.Itmg.Evidence.Persistence;
using Qec.Itmg.Evidence.Services;

namespace Qec.Itmg.Evidence;

public sealed class EvidenceModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString(QecEfConventions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{QecEfConventions.ConnectionStringName}' is not configured.");
        }

        services.AddQecSqlServerDbContext<EvidenceDbContext>(connectionString, EvidenceDbContext.SchemaName);
        services.AddScoped<EvidenceService>();
        services.AddScoped<IEvidenceCoverageQuery>(sp => sp.GetRequiredService<EvidenceService>());
        services.AddScoped<EvidenceExportService>();
    }
}
