using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.DocumentManagement.Domain;

namespace Qec.Itmg.DocumentManagement.Persistence;

public sealed class DocumentManagementDbContext(DbContextOptions<DocumentManagementDbContext> options) : DbContext(options)
{
    public const string SchemaName = "doc";

    public DbSet<ManagedDocument> ManagedDocuments => Set<ManagedDocument>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<PolicyAssignment> PolicyAssignments => Set<PolicyAssignment>();
    public DbSet<PolicyAcknowledgement> PolicyAcknowledgements => Set<PolicyAcknowledgement>();
    public DbSet<PolicyAcknowledgementReminderLog> PolicyAcknowledgementReminderLogs => Set<PolicyAcknowledgementReminderLog>();
    public DbSet<DocumentReviewNotificationLog> DocumentReviewNotificationLogs => Set<DocumentReviewNotificationLog>();
    public DbSet<DocumentGovernanceLink> DocumentGovernanceLinks => Set<DocumentGovernanceLink>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DocumentManagementDbContext).Assembly);
        QecEfConventions.Apply(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }
}
