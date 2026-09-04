using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.Contracts.Modules;
using Qec.Itmg.DocumentManagement.Persistence;
using Qec.Itmg.DocumentManagement.Services;

namespace Qec.Itmg.DocumentManagement;

public sealed class DocumentManagementModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString(QecEfConventions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{QecEfConventions.ConnectionStringName}' is not configured.");
        }

        services.Configure<DocumentManagementOptions>(configuration.GetSection(DocumentManagementOptions.SectionName));
        services.AddQecSqlServerDbContext<DocumentManagementDbContext>(
            connectionString,
            DocumentManagementDbContext.SchemaName);
        services.AddScoped<DocumentService>();
        services.AddScoped<DocumentReviewNotificationService>();
    }
}
