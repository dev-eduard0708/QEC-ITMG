using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Identity.Persistence;
using Qec.Itmg.Organization.Persistence;
using Qec.Itmg.Platform.Persistence;

namespace Qec.Itmg.Host.Persistence;

/// <summary>
/// Commits Identity + Platform (+ Organization when dirty) and any enlisted module DbContexts
/// on one shared SQL connection/transaction.
/// </summary>
public sealed class SharedSqlTransaction(
    IdentityDbContext identity,
    OrganizationDbContext organization,
    PlatformDbContext platform,
    IEnumerable<ISharedTransactionDbContext>? transactionContexts = null,
    ISharedDbConnectionAccessor? sharedConnection = null) : ISharedDbTransaction
{
    private readonly IEnumerable<ISharedTransactionDbContext> _transactionContexts =
        transactionContexts ?? [];

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

            DbTransaction dbTransaction = transaction.GetDbTransaction();
            await EnlistAsync(platform, dbTransaction, cancellationToken);
            await EnlistAsync(organization, dbTransaction, cancellationToken);

            foreach (ISharedTransactionDbContext participant in _transactionContexts)
            {
                if (participant.Database.CurrentTransaction is null)
                {
                    await participant.Database.UseTransactionAsync(dbTransaction, cancellationToken);
                }
            }

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

    private static async Task EnlistAsync(
        DbContext context,
        DbTransaction dbTransaction,
        CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null)
        {
            await context.Database.UseTransactionAsync(dbTransaction, cancellationToken);
        }
    }

    private void EnsureSameConnection()
    {
        DbConnection identityConnection = identity.Database.GetDbConnection();
        DbConnection platformConnection = platform.Database.GetDbConnection();
        DbConnection organizationConnection = organization.Database.GetDbConnection();

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
