using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.ServiceDesk.Domain;

namespace Qec.Itmg.ServiceDesk.Persistence;

public sealed class ServiceDeskDbContext(DbContextOptions<ServiceDeskDbContext> options) : DbContext(options)
{
    public const string SchemaName = "sd";

    public DbSet<Ticket> Tickets => Set<Ticket>();

    public DbSet<SupportQueue> SupportQueues => Set<SupportQueue>();

    public DbSet<SlaPolicy> SlaPolicies => Set<SlaPolicy>();

    public DbSet<TicketAssignmentHistory> TicketAssignmentHistories => Set<TicketAssignmentHistory>();

    public DbSet<TicketStatusHistory> TicketStatusHistories => Set<TicketStatusHistory>();

    public DbSet<KnowledgeArticle> KnowledgeArticles => Set<KnowledgeArticle>();

    public DbSet<Problem> Problems => Set<Problem>();

    public DbSet<ProblemIncident> ProblemIncidents => Set<ProblemIncident>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ServiceDeskDbContext).Assembly);
        QecEfConventions.Apply(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }
}
