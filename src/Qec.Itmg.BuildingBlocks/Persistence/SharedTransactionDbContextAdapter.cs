using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Qec.Itmg.BuildingBlocks.Persistence;

public sealed class SharedTransactionDbContextAdapter<TContext>(TContext db) : ISharedTransactionDbContext
    where TContext : DbContext
{
    public DatabaseFacade Database => db.Database;
}
