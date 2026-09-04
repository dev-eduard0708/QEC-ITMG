using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.ChangeManagement.Persistence;
using Qec.Itmg.ChangeManagement.Services;
using Qec.Itmg.Contracts.Modules;

namespace Qec.Itmg.ChangeManagement;

public sealed class ChangeManagementModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString(QecEfConventions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{QecEfConventions.ConnectionStringName}' is not configured.");
        }

        services.AddQecSqlServerDbContext<ChangeManagementDbContext>(
            connectionString,
            ChangeManagementDbContext.SchemaName);
        services.AddScoped<ChangeService>();
    }
}
