using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.RemoteSupport.Domain;

namespace Qec.Itmg.RemoteSupport.Persistence;

public sealed class RemoteSupportDbContext(DbContextOptions<RemoteSupportDbContext> options) : DbContext(options)
{
    public const string SchemaName = "rem";

    public DbSet<RemoteSessionRequest> RemoteSessionRequests => Set<RemoteSessionRequest>();

    public DbSet<RemoteSessionMessage> RemoteSessionMessages => Set<RemoteSessionMessage>();

    public DbSet<RemoteEndpoint> RemoteEndpoints => Set<RemoteEndpoint>();

    public DbSet<RemoteEndpointEnrollment> RemoteEndpointEnrollments => Set<RemoteEndpointEnrollment>();

    public DbSet<RemoteEndpointPairing> RemoteEndpointPairings => Set<RemoteEndpointPairing>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RemoteSupportDbContext).Assembly);
        QecEfConventions.Apply(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }
}
