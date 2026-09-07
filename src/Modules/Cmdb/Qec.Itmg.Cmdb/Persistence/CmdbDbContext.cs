using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.Cmdb.Domain;

namespace Qec.Itmg.Cmdb.Persistence;

public sealed class CmdbDbContext(DbContextOptions<CmdbDbContext> options) : DbContext(options)
{
    public const string SchemaName = "cmdb";

    public DbSet<CiType> CiTypes => Set<CiType>();

    public DbSet<ConfigurationItem> ConfigurationItems => Set<ConfigurationItem>();

    public DbSet<CiRelationship> CiRelationships => Set<CiRelationship>();

    public DbSet<Asset> Assets => Set<Asset>();

    public DbSet<AssetAssignment> AssetAssignments => Set<AssetAssignment>();

    public DbSet<BusinessService> BusinessServices => Set<BusinessService>();

    public DbSet<BusinessServiceConfigurationItem> BusinessServiceConfigurationItems =>
        Set<BusinessServiceConfigurationItem>();

    public DbSet<CiNetworkIdentity> CiNetworkIdentities => Set<CiNetworkIdentity>();

    public DbSet<NetworkTopologyView> NetworkTopologyViews => Set<NetworkTopologyView>();

    public DbSet<NetworkTopologyNodeLayout> NetworkTopologyNodeLayouts => Set<NetworkTopologyNodeLayout>();

    public DbSet<NetworkLinkDetail> NetworkLinkDetails => Set<NetworkLinkDetail>();

    public DbSet<NetworkDiscoveryProfile> NetworkDiscoveryProfiles => Set<NetworkDiscoveryProfile>();

    public DbSet<NetworkDiscoveryRun> NetworkDiscoveryRuns => Set<NetworkDiscoveryRun>();

    public DbSet<NetworkDiscoveryObservation> NetworkDiscoveryObservations => Set<NetworkDiscoveryObservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CmdbDbContext).Assembly);
        QecEfConventions.Apply(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }
}
