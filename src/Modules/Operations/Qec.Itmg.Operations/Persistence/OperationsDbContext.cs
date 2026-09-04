using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.Operations.Domain;

namespace Qec.Itmg.Operations.Persistence;

public sealed class OperationsDbContext(DbContextOptions<OperationsDbContext> options) : DbContext(options)
{
    public const string SchemaName = "ops";

    public DbSet<OperationalEvent> OperationalEvents => Set<OperationalEvent>();
    public DbSet<BackupJob> BackupJobs => Set<BackupJob>();
    public DbSet<BackupRun> BackupRuns => Set<BackupRun>();
    public DbSet<RestoreTest> RestoreTests => Set<RestoreTest>();
    public DbSet<CertificateRecord> CertificateRecords => Set<CertificateRecord>();
    public DbSet<CertificateExpiryNotificationLog> CertificateExpiryNotificationLogs => Set<CertificateExpiryNotificationLog>();
    public DbSet<PatchBaseline> PatchBaselines => Set<PatchBaseline>();
    public DbSet<PatchDeployment> PatchDeployments => Set<PatchDeployment>();
    public DbSet<ScheduledJob> ScheduledJobs => Set<ScheduledJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OperationsDbContext).Assembly);
        QecEfConventions.Apply(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }
}
