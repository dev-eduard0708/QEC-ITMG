using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qec.Itmg.ServiceDesk.Domain;

namespace Qec.Itmg.ServiceDesk.Persistence.Configurations;

internal sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("Ticket");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.TicketNumber)
            .IsRequired()
            .HasMaxLength(32)
            .HasColumnType("nvarchar(32)");

        builder.Property(item => item.Type)
            .IsRequired()
            .HasConversion<string>()
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

        builder.Property(item => item.Category).HasMaxLength(128).HasColumnType("nvarchar(128)");

        builder.Property(item => item.IsMajorIncident).IsRequired();

        builder.Property(item => item.SecurityClassification)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasColumnType("nvarchar(32)");

        builder.Property(item => item.SourceEventId);

        builder.Property(item => item.RequesterUserId).IsRequired();
        builder.Property(item => item.CreatedAtUtc).IsRequired();
        builder.Property(item => item.UpdatedAtUtc).IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();

        builder.HasIndex(item => item.TicketNumber)
            .IsUnique()
            .HasDatabaseName("IX_Ticket_TicketNumber");

        builder.HasIndex(item => new { item.Status, item.UpdatedAtUtc })
            .HasDatabaseName("IX_Ticket_Status_UpdatedAtUtc");

        builder.HasIndex(item => item.RequesterUserId)
            .HasDatabaseName("IX_Ticket_RequesterUserId");

        builder.HasIndex(item => item.QueueId)
            .HasDatabaseName("IX_Ticket_QueueId");

        builder.HasIndex(item => item.SourceEventId)
            .IsUnique()
            .HasFilter("[SourceEventId] IS NOT NULL")
            .HasDatabaseName("IX_Ticket_SourceEventId");

        builder.HasOne<SupportQueue>()
            .WithMany()
            .HasForeignKey(item => item.QueueId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<SlaPolicy>()
            .WithMany()
            .HasForeignKey(item => item.SlaPolicyId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class SupportQueueConfiguration : IEntityTypeConfiguration<SupportQueue>
{
    public void Configure(EntityTypeBuilder<SupportQueue> builder)
    {
        builder.ToTable("SupportQueue");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.Name)
            .IsRequired()
            .HasMaxLength(128)
            .HasColumnType("nvarchar(128)");

        builder.Property(item => item.Description).HasMaxLength(512).HasColumnType("nvarchar(512)");
        builder.Property(item => item.CreatedAtUtc).IsRequired();
        builder.Property(item => item.UpdatedAtUtc).IsRequired();

        builder.HasIndex(item => item.Name)
            .IsUnique()
            .HasDatabaseName("IX_SupportQueue_Name");
    }
}

internal sealed class SlaPolicyConfiguration : IEntityTypeConfiguration<SlaPolicy>
{
    public void Configure(EntityTypeBuilder<SlaPolicy> builder)
    {
        builder.ToTable("SlaPolicy");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.Name)
            .IsRequired()
            .HasMaxLength(128)
            .HasColumnType("nvarchar(128)");

        builder.Property(item => item.TicketType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasColumnType("nvarchar(32)");

        builder.Property(item => item.Priority)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasColumnType("nvarchar(32)");

        builder.Property(item => item.CreatedAtUtc).IsRequired();
        builder.Property(item => item.UpdatedAtUtc).IsRequired();

        builder.HasIndex(item => new { item.Priority, item.TicketType, item.IsActive })
            .HasDatabaseName("IX_SlaPolicy_Priority_TicketType_IsActive");
    }
}

internal sealed class TicketAssignmentHistoryConfiguration : IEntityTypeConfiguration<TicketAssignmentHistory>
{
    public void Configure(EntityTypeBuilder<TicketAssignmentHistory> builder)
    {
        builder.ToTable("TicketAssignmentHistory");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.Notes).HasMaxLength(1024).HasColumnType("nvarchar(1024)");
        builder.Property(item => item.AssignedAtUtc).IsRequired();
        builder.Property(item => item.AssignedByUserId).IsRequired();

        builder.HasIndex(item => new { item.TicketId, item.AssignedAtUtc })
            .HasDatabaseName("IX_TicketAssignmentHistory_TicketId_AssignedAtUtc");

        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(item => item.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class TicketStatusHistoryConfiguration : IEntityTypeConfiguration<TicketStatusHistory>
{
    public void Configure(EntityTypeBuilder<TicketStatusHistory> builder)
    {
        builder.ToTable("TicketStatusHistory");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.FromStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasColumnType("nvarchar(32)");

        builder.Property(item => item.ToStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasColumnType("nvarchar(32)");

        builder.Property(item => item.ChangedByUserId).IsRequired();
        builder.Property(item => item.ChangedAtUtc).IsRequired();

        builder.HasIndex(item => new { item.TicketId, item.ChangedAtUtc })
            .HasDatabaseName("IX_TicketStatusHistory_TicketId_ChangedAtUtc");

        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(item => item.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
