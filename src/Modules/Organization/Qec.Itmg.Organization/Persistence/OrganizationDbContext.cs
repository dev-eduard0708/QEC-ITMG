using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.Organization.Domain;

namespace Qec.Itmg.Organization.Persistence;

public sealed class OrganizationDbContext(DbContextOptions<OrganizationDbContext> options) : DbContext(options)
{
    public const string SchemaName = "org";

    public DbSet<Department> Departments => Set<Department>();

    public DbSet<Location> Locations => Set<Location>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrganizationDbContext).Assembly);
        QecEfConventions.Apply(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }
}
