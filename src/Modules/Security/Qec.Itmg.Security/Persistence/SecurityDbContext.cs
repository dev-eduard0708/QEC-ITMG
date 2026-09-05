using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.Security.Domain;

namespace Qec.Itmg.Security.Persistence;

public sealed class SecurityDbContext(DbContextOptions<SecurityDbContext> options) : DbContext(options)
{
    public const string SchemaName = "sec";

    public DbSet<Vulnerability> Vulnerabilities => Set<Vulnerability>();
    public DbSet<VulnerabilityRemediationLink> VulnerabilityRemediationLinks => Set<VulnerabilityRemediationLink>();
    public DbSet<Risk> Risks => Set<Risk>();
    public DbSet<RiskLink> RiskLinks => Set<RiskLink>();
    public DbSet<PolicyException> PolicyExceptions => Set<PolicyException>();
    public DbSet<PenetrationTest> PenetrationTests => Set<PenetrationTest>();
    public DbSet<PentestFinding> PentestFindings => Set<PentestFinding>();
    public DbSet<AwarenessCampaign> AwarenessCampaigns => Set<AwarenessCampaign>();
    public DbSet<AwarenessCompletion> AwarenessCompletions => Set<AwarenessCompletion>();
    public DbSet<AwarenessModule> AwarenessModules => Set<AwarenessModule>();
    public DbSet<AwarenessQuestion> AwarenessQuestions => Set<AwarenessQuestion>();
    public DbSet<AwarenessAnswerOption> AwarenessAnswerOptions => Set<AwarenessAnswerOption>();
    public DbSet<AwarenessAttempt> AwarenessAttempts => Set<AwarenessAttempt>();
    public DbSet<AwarenessReminderLog> AwarenessReminderLogs => Set<AwarenessReminderLog>();
    public DbSet<ExceptionExpiryNotificationLog> ExceptionExpiryNotificationLogs => Set<ExceptionExpiryNotificationLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SecurityDbContext).Assembly);
        QecEfConventions.Apply(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }
}
