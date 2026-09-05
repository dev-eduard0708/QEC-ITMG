using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.Contracts.Modules;
using Qec.Itmg.Reporting.Persistence;
using Qec.Itmg.Reporting.Services;

namespace Qec.Itmg.Reporting;

public sealed class ReportingModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString(QecEfConventions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{QecEfConventions.ConnectionStringName}' is not configured.");
        }

        services.AddQecSqlServerDbContext<ReportingDbContext>(connectionString, ReportingDbContext.SchemaName);
        services.AddScoped<ReportSnapshotService>();
    }
}
