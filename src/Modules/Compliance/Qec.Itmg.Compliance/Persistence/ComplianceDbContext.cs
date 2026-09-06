using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.Compliance.Domain;

namespace Qec.Itmg.Compliance.Persistence;

public sealed class ComplianceDbContext(DbContextOptions<ComplianceDbContext> options) : DbContext(options)
{
    public const string SchemaName = "cmp";

    public DbSet<Framework> Frameworks => Set<Framework>();
    public DbSet<FrameworkVersion> FrameworkVersions => Set<FrameworkVersion>();
    public DbSet<FrameworkRequirement> FrameworkRequirements => Set<FrameworkRequirement>();
    public DbSet<ControlMapping> ControlMappings => Set<ControlMapping>();
    public DbSet<ControlAssessment> ControlAssessments => Set<ControlAssessment>();
    public DbSet<ComplianceCalendarItem> ComplianceCalendarItems => Set<ComplianceCalendarItem>();
    public DbSet<FrameworkRequirementApplicability> FrameworkRequirementApplicabilities =>
        Set<FrameworkRequirementApplicability>();
    public DbSet<FrameworkRequirementOperationalLink> FrameworkRequirementOperationalLinks =>
        Set<FrameworkRequirementOperationalLink>();
    public DbSet<FrameworkTranslation> FrameworkTranslations => Set<FrameworkTranslation>();
    public DbSet<FrameworkRequirementTranslation> FrameworkRequirementTranslations =>
        Set<FrameworkRequirementTranslation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ComplianceDbContext).Assembly);
        QecEfConventions.Apply(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }
}
