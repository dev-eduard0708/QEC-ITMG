using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Persistence;

namespace Qec.Itmg.Platform.Persistence;

public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options) : DbContext(options)
{
    public const string SchemaName = "plt";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        QecEfConventions.Apply(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }
}
