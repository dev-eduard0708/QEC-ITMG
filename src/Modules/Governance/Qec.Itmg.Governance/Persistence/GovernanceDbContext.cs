using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.Governance.Domain;

namespace Qec.Itmg.Governance.Persistence;

public sealed class GovernanceDbContext(DbContextOptions<GovernanceDbContext> options) : DbContext(options)
{
    public const string SchemaName = "gov";

    public DbSet<OrganizationProfile> OrganizationProfiles => Set<OrganizationProfile>();
    public DbSet<OrganizationalUnit> OrganizationalUnits => Set<OrganizationalUnit>();
    public DbSet<OrganizationalUnitMembership> OrganizationalUnitMemberships => Set<OrganizationalUnitMembership>();
    public DbSet<InternalControl> InternalControls => Set<InternalControl>();
    public DbSet<ControlSecondaryOwner> ControlSecondaryOwners => Set<ControlSecondaryOwner>();
    public DbSet<ControlConfigurationItemLink> ControlConfigurationItemLinks => Set<ControlConfigurationItemLink>();
    public DbSet<ControlBusinessServiceLink> ControlBusinessServiceLinks => Set<ControlBusinessServiceLink>();
    public DbSet<ControlManagedDocumentLink> ControlManagedDocumentLinks => Set<ControlManagedDocumentLink>();
    public DbSet<ControlTestProcedure> ControlTestProcedures => Set<ControlTestProcedure>();
    public DbSet<EvidenceRequirement> EvidenceRequirements => Set<EvidenceRequirement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GovernanceDbContext).Assembly);
        QecEfConventions.Apply(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }
}
