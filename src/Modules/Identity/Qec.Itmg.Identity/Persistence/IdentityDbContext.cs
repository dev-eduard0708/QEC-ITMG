using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Persistence;

namespace Qec.Itmg.Identity.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public const string SchemaName = "id";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        QecEfConventions.Apply(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }
}
