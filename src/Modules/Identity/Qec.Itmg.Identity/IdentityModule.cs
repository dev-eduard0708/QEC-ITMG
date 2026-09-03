using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Contracts.Modules;
using Qec.Itmg.Identity.Audit;
using Qec.Itmg.Identity.Persistence;

namespace Qec.Itmg.Identity;

/// <summary>
/// Identity module composition: persistence and authentication helpers.
/// </summary>
public sealed class IdentityModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString(QecEfConventions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{QecEfConventions.ConnectionStringName}' is not configured.");
        }

        services.AddDbContext<IdentityDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", IdentityDbContext.SchemaName)));

        services.AddHttpContextAccessor();
        services.AddScoped<IAuditRequestContext, IdentityAuditRequestContext>();
    }
}
