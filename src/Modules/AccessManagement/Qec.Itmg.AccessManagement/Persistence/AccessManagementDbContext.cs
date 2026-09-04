using Microsoft.EntityFrameworkCore;
using Qec.Itmg.AccessManagement.Domain;
using Qec.Itmg.BuildingBlocks.Persistence;

namespace Qec.Itmg.AccessManagement.Persistence;

public sealed class AccessManagementDbContext(DbContextOptions<AccessManagementDbContext> options) : DbContext(options)
{
    public const string SchemaName = "acc";

    public DbSet<AccessCase> AccessCases => Set<AccessCase>();
    public DbSet<AccessCaseItem> AccessCaseItems => Set<AccessCaseItem>();
    public DbSet<ExistingAccessSnapshotItem> ExistingAccessSnapshotItems => Set<ExistingAccessSnapshotItem>();
    public DbSet<AccessCaseException> AccessCaseExceptions => Set<AccessCaseException>();
    public DbSet<AccessReviewCampaign> AccessReviewCampaigns => Set<AccessReviewCampaign>();
    public DbSet<AccessReviewItem> AccessReviewItems => Set<AccessReviewItem>();
    public DbSet<ManagedAccount> ManagedAccounts => Set<ManagedAccount>();
    public DbSet<SodRule> SodRules => Set<SodRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AccessManagementDbContext).Assembly);
        QecEfConventions.Apply(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }
}
