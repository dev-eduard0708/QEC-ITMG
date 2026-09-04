using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.Contracts.Modules;
using Qec.Itmg.ServiceDesk.Persistence;
using Qec.Itmg.ServiceDesk.Services;

namespace Qec.Itmg.ServiceDesk;

public sealed class ServiceDeskModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString(QecEfConventions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{QecEfConventions.ConnectionStringName}' is not configured.");
        }

        services.AddQecSqlServerDbContext<ServiceDeskDbContext>(
            connectionString,
            ServiceDeskDbContext.SchemaName);
        services.AddScoped<TicketService>();
        services.AddScoped<SlaEvaluationService>();
        services.AddScoped<KnowledgeArticleService>();
        services.AddScoped<ProblemService>();
    }
}
