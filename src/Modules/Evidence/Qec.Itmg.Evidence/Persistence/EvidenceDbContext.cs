using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.Evidence.Domain;

namespace Qec.Itmg.Evidence.Persistence;

public sealed class EvidenceDbContext(DbContextOptions<EvidenceDbContext> options) : DbContext(options)
{
    public const string SchemaName = "evd";

    public DbSet<EvidenceRecord> EvidenceRecords => Set<EvidenceRecord>();
    public DbSet<EvidenceVersion> EvidenceVersions => Set<EvidenceVersion>();
    public DbSet<EvidenceLink> EvidenceLinks => Set<EvidenceLink>();
    public DbSet<EvidenceExpiryNotificationLog> EvidenceExpiryNotificationLogs => Set<EvidenceExpiryNotificationLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EvidenceDbContext).Assembly);
        QecEfConventions.Apply(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }
}
