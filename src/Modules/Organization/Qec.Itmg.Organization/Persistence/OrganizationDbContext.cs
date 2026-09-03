using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Persistence;

namespace Qec.Itmg.Organization.Persistence;

public sealed class OrganizationDbContext(DbContextOptions<OrganizationDbContext> options) : DbContext(options)
{
    public const string SchemaName = "org";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        QecEfConventions.Apply(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }
}
