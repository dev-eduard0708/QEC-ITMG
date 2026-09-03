using System.Data;
using System.Data.Common;

namespace Qec.Itmg.BuildingBlocks.Persistence;

/// <summary>
/// Request-scoped relational connection shared by module DbContexts.
/// Registered by the composition root (Host); modules resolve it optionally.
/// </summary>
public interface ISharedDbConnectionAccessor : IAsyncDisposable, IDisposable
{
    DbConnection Connection { get; }

    Task EnsureOpenAsync(CancellationToken cancellationToken = default);
}
