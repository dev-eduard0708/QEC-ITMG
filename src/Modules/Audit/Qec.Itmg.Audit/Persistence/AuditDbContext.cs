using Microsoft.EntityFrameworkCore;
using Qec.Itmg.Audit.Domain;
using Qec.Itmg.BuildingBlocks.Persistence;

namespace Qec.Itmg.Audit.Persistence;

public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options)
{
    public const string SchemaName = "aud";

    public DbSet<AuditEngagement> AuditEngagements => Set<AuditEngagement>();
    public DbSet<AuditScopeLink> AuditScopeLinks => Set<AuditScopeLink>();
    public DbSet<AuditQuestion> AuditQuestions => Set<AuditQuestion>();
    public DbSet<Finding> Findings => Set<Finding>();
    public DbSet<ManagementResponse> ManagementResponses => Set<ManagementResponse>();
    public DbSet<CorrectiveAction> CorrectiveActions => Set<CorrectiveAction>();
    public DbSet<EvidenceRequest> EvidenceRequests => Set<EvidenceRequest>();
    public DbSet<EvidenceRequestNotificationLog> EvidenceRequestNotificationLogs => Set<EvidenceRequestNotificationLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditDbContext).Assembly);
        QecEfConventions.Apply(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }
}
