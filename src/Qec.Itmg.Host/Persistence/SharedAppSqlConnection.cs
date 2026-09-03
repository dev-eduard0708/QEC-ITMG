using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Qec.Itmg.BuildingBlocks.Persistence;

namespace Qec.Itmg.Host.Persistence;

/// <summary>
/// Scoped SQL Server connection shared by Identity, Organization, and Platform DbContexts.
/// </summary>
public sealed class SharedAppSqlConnection : ISharedDbConnectionAccessor
{
    private readonly SqlConnection _connection;
    private bool _disposed;

    public SharedAppSqlConnection(IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString(QecEfConventions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{QecEfConventions.ConnectionStringName}' is not configured.");
        }

        _connection = new SqlConnection(connectionString);
    }

    public DbConnection Connection => _connection;

    public async Task EnsureOpenAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_connection.State != ConnectionState.Open)
        {
            await _connection.OpenAsync(cancellationToken);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _connection.Dispose();
        _disposed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _connection.DisposeAsync();
        _disposed = true;
    }
}
