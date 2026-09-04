using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.Operations.Domain;

namespace Qec.Itmg.Operations.Persistence;

public sealed class OperationsDbContext(DbContextOptions<OperationsDbContext> options) : DbContext(options)
{
    public const string SchemaName = "ops";

    public DbSet<OperationalEvent> OperationalEvents => Set<OperationalEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OperationsDbContext).Assembly);
        QecEfConventions.Apply(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }
}
