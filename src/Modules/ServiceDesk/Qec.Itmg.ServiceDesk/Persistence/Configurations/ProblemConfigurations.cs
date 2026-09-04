using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qec.Itmg.ServiceDesk.Domain;

namespace Qec.Itmg.ServiceDesk.Persistence.Configurations;

internal sealed class ProblemConfiguration : IEntityTypeConfiguration<Problem>
{
    public void Configure(EntityTypeBuilder<Problem> builder)
    {
        builder.ToTable("Problem");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.ProblemNumber)
            .IsRequired()
            .HasMaxLength(32)
            .HasColumnType("nvarchar(32)");

        builder.Property(item => item.Title)
            .IsRequired()
            .HasMaxLength(256)
            .HasColumnType("nvarchar(256)");

        builder.Property(item => item.Description)
            .IsRequired()
            .HasMaxLength(4000)
            .HasColumnType("nvarchar(4000)");

        builder.Property(item => item.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasColumnType("nvarchar(32)");

        builder.Property(item => item.Priority)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasColumnType("nvarchar(32)");

        builder.Property(item => item.RootCause).HasMaxLength(4000).HasColumnType("nvarchar(4000)");
        builder.Property(item => item.Workaround).HasMaxLength(4000).HasColumnType("nvarchar(4000)");
        builder.Property(item => item.CreatedAtUtc).IsRequired();
        builder.Property(item => item.UpdatedAtUtc).IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();

        builder.HasIndex(item => item.ProblemNumber)
            .IsUnique()
            .HasDatabaseName("IX_Problem_ProblemNumber");

        builder.HasIndex(item => new { item.Status, item.UpdatedAtUtc })
            .HasDatabaseName("IX_Problem_Status_UpdatedAtUtc");
    }
}

internal sealed class ProblemIncidentConfiguration : IEntityTypeConfiguration<ProblemIncident>
{
    public void Configure(EntityTypeBuilder<ProblemIncident> builder)
    {
        builder.ToTable("ProblemIncident");
        builder.HasKey(item => new { item.ProblemId, item.IncidentTicketId });

        builder.Property(item => item.LinkedAtUtc).IsRequired();
        builder.Property(item => item.LinkedByUserId).IsRequired();

        builder.HasIndex(item => item.IncidentTicketId)
            .HasDatabaseName("IX_ProblemIncident_IncidentTicketId");

        builder.HasOne<Problem>()
            .WithMany()
            .HasForeignKey(item => item.ProblemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(item => item.IncidentTicketId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
