using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.Contracts.Modules;
using Qec.Itmg.Compliance.Persistence;
using Qec.Itmg.Compliance.Services;

namespace Qec.Itmg.Compliance;

public sealed class ComplianceModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString(QecEfConventions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{QecEfConventions.ConnectionStringName}' is not configured.");
        }

        services.AddQecSqlServerDbContext<ComplianceDbContext>(
            connectionString,
            ComplianceDbContext.SchemaName);
        services.AddScoped<FrameworkService>();
        services.AddScoped<ControlMappingService>();
        services.AddScoped<CoverageService>();
        services.AddScoped<ControlAssessmentService>();
        services.AddScoped<ComplianceCalendarService>();
        services.AddScoped<FrameworkImportService>();
        services.AddScoped<FrameworkStructureSeedService>();
    }
}
