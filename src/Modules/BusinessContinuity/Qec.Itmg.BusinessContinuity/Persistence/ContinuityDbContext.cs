using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.BusinessContinuity.Domain;

namespace Qec.Itmg.BusinessContinuity.Persistence;

public sealed class ContinuityDbContext(DbContextOptions<ContinuityDbContext> options) : DbContext(options)
{
    public const string SchemaName = "bcm";

    public DbSet<BiaRecord> BiaRecords => Set<BiaRecord>();
    public DbSet<ContinuityPlan> ContinuityPlans => Set<ContinuityPlan>();
    public DbSet<ContinuityScopeLink> ContinuityScopeLinks => Set<ContinuityScopeLink>();
    public DbSet<RecoveryProcedure> RecoveryProcedures => Set<RecoveryProcedure>();
    public DbSet<DrTest> DrTests => Set<DrTest>();
    public DbSet<ContinuityNotificationLog> ContinuityNotificationLogs => Set<ContinuityNotificationLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ContinuityDbContext).Assembly);
        QecEfConventions.Apply(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }
}
