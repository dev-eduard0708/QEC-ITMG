using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.ThirdParty.Domain;

namespace Qec.Itmg.ThirdParty.Persistence;

public sealed class ThirdPartyDbContext(DbContextOptions<ThirdPartyDbContext> options) : DbContext(options)
{
    public const string SchemaName = "tpm";

    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<VendorContact> VendorContacts => Set<VendorContact>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<VendorAssessment> VendorAssessments => Set<VendorAssessment>();
    public DbSet<VendorScopeLink> VendorScopeLinks => Set<VendorScopeLink>();
    public DbSet<VendorNotificationLog> VendorNotificationLogs => Set<VendorNotificationLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ThirdPartyDbContext).Assembly);
        QecEfConventions.Apply(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }
}
