using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.ChangeManagement.Domain;

namespace Qec.Itmg.ChangeManagement.Persistence;

public sealed class ChangeManagementDbContext(DbContextOptions<ChangeManagementDbContext> options) : DbContext(options)
{
    public const string SchemaName = "chg";

    public DbSet<ChangeRequest> ChangeRequests => Set<ChangeRequest>();
    public DbSet<ChangeConfigurationItem> ChangeConfigurationItems => Set<ChangeConfigurationItem>();
    public DbSet<ChangeApproval> ChangeApprovals => Set<ChangeApproval>();
    public DbSet<ChangeStatusHistory> ChangeStatusHistories => Set<ChangeStatusHistory>();
    public DbSet<StandardChangeCatalogItem> StandardChangeCatalogItems => Set<StandardChangeCatalogItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ChangeManagementDbContext).Assembly);
        QecEfConventions.Apply(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }
}
