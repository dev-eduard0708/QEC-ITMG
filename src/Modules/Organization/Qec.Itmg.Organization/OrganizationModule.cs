using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.Contracts.Modules;
using Qec.Itmg.Organization.Persistence;

namespace Qec.Itmg.Organization;

/// <summary>
/// Organization module composition. Domain types are added in Phase 1.
/// </summary>
public sealed class OrganizationModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString(QecEfConventions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{QecEfConventions.ConnectionStringName}' is not configured.");
        }

        services.AddDbContext<OrganizationDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", OrganizationDbContext.SchemaName)));
    }
}
