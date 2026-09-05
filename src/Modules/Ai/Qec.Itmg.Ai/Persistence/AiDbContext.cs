using Microsoft.EntityFrameworkCore;
using Qec.Itmg.Ai.Domain;
using Qec.Itmg.BuildingBlocks.Persistence;

namespace Qec.Itmg.Ai.Persistence;

public sealed class AiDbContext(DbContextOptions<AiDbContext> options) : DbContext(options)
{
    public const string SchemaName = "ai";

    public DbSet<AiInteraction> Interactions => Set<AiInteraction>();
    public DbSet<AiToolInvocation> ToolInvocations => Set<AiToolInvocation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AiDbContext).Assembly);
        QecEfConventions.Apply(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }
}
