using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qec.Itmg.AccessManagement.Persistence;
using Qec.Itmg.AccessManagement.Services;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.Contracts.Modules;

namespace Qec.Itmg.AccessManagement;

public sealed class AccessManagementModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString(QecEfConventions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{QecEfConventions.ConnectionStringName}' is not configured.");
        }

        services.AddQecSqlServerDbContext<AccessManagementDbContext>(
            connectionString,
            AccessManagementDbContext.SchemaName);
        services.AddScoped<AccessCaseService>();
        services.AddScoped<AccessReviewService>();
        services.AddScoped<ManagedAccountService>();
        services.AddScoped<SodService>();
        services.AddScoped<AccessEvidenceService>();
    }
}
