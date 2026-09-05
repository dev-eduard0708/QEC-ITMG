using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qec.Itmg.RemoteSupport.Domain;

namespace Qec.Itmg.RemoteSupport.Persistence.Configurations;

internal sealed class RemoteSessionRequestConfiguration : IEntityTypeConfiguration<RemoteSessionRequest>
{
    public void Configure(EntityTypeBuilder<RemoteSessionRequest> builder)
    {
        builder.ToTable("RemoteSessionRequest");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RemoteNumber).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Reason).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.RequestedPrivileges).HasMaxLength(512);
        builder.Property(x => x.SessionType).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.EngineSessionId).HasMaxLength(128);
        builder.Property(x => x.EngineJoinUrl).HasMaxLength(1024);
        builder.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(64);
        builder.Property(x => x.EndReason).HasMaxLength(512);
        builder.Property(x => x.ConsentIpAddress).HasMaxLength(64);
        builder.Property(x => x.RecordingReference).HasMaxLength(512);
        builder.Property(x => x.LastEngineError).HasMaxLength(1000);
        builder.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();

        builder.HasIndex(x => x.RemoteNumber).IsUnique().HasDatabaseName("IX_RemoteSessionRequest_RemoteNumber");
        builder.HasIndex(x => x.Status).HasDatabaseName("IX_RemoteSessionRequest_Status");
        builder.HasIndex(x => x.ConfigurationItemId).HasDatabaseName("IX_RemoteSessionRequest_ConfigurationItemId");
        builder.HasIndex(x => x.TicketId).HasDatabaseName("IX_RemoteSessionRequest_TicketId");
        builder.HasIndex(x => x.ChangeRequestId).HasDatabaseName("IX_RemoteSessionRequest_ChangeRequestId");
        builder.HasIndex(x => x.TargetUserId).HasDatabaseName("IX_RemoteSessionRequest_TargetUserId");
        builder.HasIndex(x => x.TechnicianUserId).HasDatabaseName("IX_RemoteSessionRequest_TechnicianUserId");
        builder.HasIndex(x => x.EngineSessionId).HasDatabaseName("IX_RemoteSessionRequest_EngineSessionId");
        builder.HasIndex(x => x.RequestedAtUtc).HasDatabaseName("IX_RemoteSessionRequest_RequestedAtUtc");
        builder.HasIndex(x => new { x.Status, x.StartedAtUtc })
            .HasDatabaseName("IX_RemoteSessionRequest_ActiveLookup");
    }
}

internal sealed class RemoteSessionMessageConfiguration : IEntityTypeConfiguration<RemoteSessionMessage>
{
    public void Configure(EntityTypeBuilder<RemoteSessionMessage> builder)
    {
        builder.ToTable("RemoteSessionMessage");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MessageText).IsRequired().HasMaxLength(4000);
        builder.Property(x => x.MessageType).IsRequired().HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.SystemEventKey).HasMaxLength(64);
        builder.HasIndex(x => new { x.RemoteSessionRequestId, x.SentAtUtc })
            .HasDatabaseName("IX_RemoteSessionMessage_Session_Sent");
        builder.HasOne<RemoteSessionRequest>()
            .WithMany()
            .HasForeignKey(x => x.RemoteSessionRequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
