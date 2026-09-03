using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.Cmdb.Domain;

namespace Qec.Itmg.Cmdb.Persistence;

public sealed class CmdbDbContext(DbContextOptions<CmdbDbContext> options) : DbContext(options)
{
    public const string SchemaName = "cmdb";

    public DbSet<CiType> CiTypes => Set<CiType>();

    public DbSet<ConfigurationItem> ConfigurationItems => Set<ConfigurationItem>();

    public DbSet<CiRelationship> CiRelationships => Set<CiRelationship>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CmdbDbContext).Assembly);
        QecEfConventions.Apply(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }
}
