using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Identity.Persistence;
using Qec.Itmg.Organization.Persistence;
using Qec.Itmg.Platform.Persistence;

namespace Qec.Itmg.Host.Persistence;

/// <summary>
/// Commits Identity + Platform (+ Organization when dirty) on one shared SQL connection/transaction.
/// </summary>
public sealed class SharedSqlTransaction(
    IdentityDbContext identity,
    OrganizationDbContext organization,
    PlatformDbContext platform,
    ISharedDbConnectionAccessor? sharedConnection = null) : ISharedDbTransaction
{
    public async Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        if (!identity.Database.IsRelational())
        {
            await work(cancellationToken);
            // Persist audit first so a failing audit write leaves Identity uncommitted in non-relational providers.
            await platform.SaveChangesAsync(cancellationToken);
            await identity.SaveChangesAsync(cancellationToken);
            if (organization.ChangeTracker.HasChanges())
            {
                await organization.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        if (sharedConnection is not null)
        {
            await sharedConnection.EnsureOpenAsync(cancellationToken);
            EnsureSameConnection();
        }

        IExecutionStrategy strategy = identity.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using IDbContextTransaction transaction =
                await identity.Database.BeginTransactionAsync(cancellationToken);

            await platform.Database.UseTransactionAsync(transaction.GetDbTransaction(), cancellationToken);
            await organization.Database.UseTransactionAsync(transaction.GetDbTransaction(), cancellationToken);

            try
            {
                await work(cancellationToken);
                await identity.SaveChangesAsync(cancellationToken);
                await platform.SaveChangesAsync(cancellationToken);
                if (organization.ChangeTracker.HasChanges())
                {
                    await organization.SaveChangesAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private void EnsureSameConnection()
    {
        System.Data.Common.DbConnection identityConnection = identity.Database.GetDbConnection();
        System.Data.Common.DbConnection platformConnection = platform.Database.GetDbConnection();
        System.Data.Common.DbConnection organizationConnection = organization.Database.GetDbConnection();

        if (!ReferenceEquals(identityConnection, platformConnection)
            || !ReferenceEquals(identityConnection, organizationConnection))
        {
            throw new InvalidOperationException(
                "Module DbContexts must share one relational DbConnection for SharedSqlTransaction.");
        }

        if (sharedConnection is not null && !ReferenceEquals(identityConnection, sharedConnection.Connection))
        {
            throw new InvalidOperationException(
                "Module DbContexts are not using the scoped shared SQL connection.");
        }
    }
}
