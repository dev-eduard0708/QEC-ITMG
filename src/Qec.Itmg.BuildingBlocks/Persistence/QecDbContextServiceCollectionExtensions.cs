using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Qec.Itmg.BuildingBlocks.Persistence;

/// <summary>
/// Registers module DbContexts so they reuse the Host-provided shared SQL connection when present.
/// </summary>
public static class QecDbContextServiceCollectionExtensions
{
    public static IServiceCollection AddQecSqlServerDbContext<TContext>(
        this IServiceCollection services,
        string connectionString,
        string migrationsHistorySchema)
        where TContext : DbContext
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(migrationsHistorySchema);

        services.AddDbContext<TContext>((serviceProvider, options) =>
        {
            ISharedDbConnectionAccessor? shared = serviceProvider.GetService<ISharedDbConnectionAccessor>();
            if (shared is not null)
            {
                options.UseSqlServer(
                    shared.Connection,
                    sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", migrationsHistorySchema));
            }
            else
            {
                options.UseSqlServer(
                    connectionString,
                    sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", migrationsHistorySchema));
            }
        });

        return services;
    }
}
