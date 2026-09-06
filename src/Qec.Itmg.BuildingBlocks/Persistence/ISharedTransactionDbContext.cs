using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Qec.Itmg.BuildingBlocks.Persistence;

/// <summary>
/// Module DbContext that must enlist in <c>ISharedDbTransaction</c> when using the scoped shared SQL connection.
/// </summary>
public interface ISharedTransactionDbContext
{
    DatabaseFacade Database { get; }
}
