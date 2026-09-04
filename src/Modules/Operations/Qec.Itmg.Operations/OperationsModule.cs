using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.Contracts.Modules;
using Qec.Itmg.Operations.Persistence;
using Qec.Itmg.Operations.Services;

namespace Qec.Itmg.Operations;

public sealed class OperationsModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString(QecEfConventions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{QecEfConventions.ConnectionStringName}' is not configured.");
        }

        services.AddQecSqlServerDbContext<OperationsDbContext>(
            connectionString,
            OperationsDbContext.SchemaName);
        services.AddScoped<EventService>();
    }
}
