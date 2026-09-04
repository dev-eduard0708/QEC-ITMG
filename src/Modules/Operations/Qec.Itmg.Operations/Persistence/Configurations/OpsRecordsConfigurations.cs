using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qec.Itmg.Operations.Domain;

namespace Qec.Itmg.Operations.Persistence.Configurations;

internal sealed class BackupJobConfiguration : IEntityTypeConfiguration<BackupJob>
{
    public void Configure(EntityTypeBuilder<BackupJob> builder)
    {
        builder.ToTable("BackupJob");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Provider).IsRequired().HasMaxLength(64);
        builder.Property(x => x.ExternalJobId).HasMaxLength(128);
        builder.HasIndex(x => new { x.Provider, x.Name }).HasDatabaseName("IX_BackupJob_Provider_Name");
        builder.HasIndex(x => x.IsActive).HasDatabaseName("IX_BackupJob_IsActive");
    }
}

internal sealed class BackupRunConfiguration : IEntityTypeConfiguration<BackupRun>
{
    public void Configure(EntityTypeBuilder<BackupRun> builder)
    {
        builder.ToTable("BackupRun");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Summary).HasMaxLength(2000);
        builder.Property(x => x.ExternalReference).HasMaxLength(256);
        builder.HasIndex(x => new { x.BackupJobId, x.StartedAtUtc }).HasDatabaseName("IX_BackupRun_Job_Started");
        builder.HasIndex(x => x.Status).HasDatabaseName("IX_BackupRun_Status");
        builder.HasOne<BackupJob>().WithMany().HasForeignKey(x => x.BackupJobId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class RestoreTestConfiguration : IEntityTypeConfiguration<RestoreTest>
{
    public void Configure(EntityTypeBuilder<RestoreTest> builder)
    {
        builder.ToTable("RestoreTest");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Result).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.HasIndex(x => x.Result).HasDatabaseName("IX_RestoreTest_Result");
        builder.HasIndex(x => x.ScheduledAtUtc).HasDatabaseName("IX_RestoreTest_ScheduledAtUtc");
    }
}

internal sealed class CertificateRecordConfiguration : IEntityTypeConfiguration<CertificateRecord>
{
    public void Configure(EntityTypeBuilder<CertificateRecord> builder)
    {
        builder.ToTable("CertificateRecord");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Subject).HasMaxLength(512);
        builder.Property(x => x.Issuer).HasMaxLength(512);
        builder.Property(x => x.Thumbprint).HasMaxLength(128);
        builder.HasIndex(x => x.ExpiresAtUtc).HasDatabaseName("IX_CertificateRecord_ExpiresAtUtc");
        builder.HasIndex(x => x.IsActive).HasDatabaseName("IX_CertificateRecord_IsActive");
        builder.HasIndex(x => x.Thumbprint).HasDatabaseName("IX_CertificateRecord_Thumbprint");
    }
}

internal sealed class CertificateExpiryNotificationLogConfiguration : IEntityTypeConfiguration<CertificateExpiryNotificationLog>
{
    public void Configure(EntityTypeBuilder<CertificateExpiryNotificationLog> builder)
    {
        builder.ToTable("CertificateExpiryNotificationLog");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.CertificateId, x.ThresholdDays })
            .IsUnique()
            .HasDatabaseName("IX_CertificateExpiryNotificationLog_Cert_Threshold");
        builder.HasOne<CertificateRecord>().WithMany().HasForeignKey(x => x.CertificateId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PatchBaselineConfiguration : IEntityTypeConfiguration<PatchBaseline>
{
    public void Configure(EntityTypeBuilder<PatchBaseline> builder)
    {
        builder.ToTable("PatchBaseline");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.Version).HasMaxLength(64);
        builder.HasIndex(x => x.IsActive).HasDatabaseName("IX_PatchBaseline_IsActive");
    }
}

internal sealed class PatchDeploymentConfiguration : IEntityTypeConfiguration<PatchDeployment>
{
    public void Configure(EntityTypeBuilder<PatchDeployment> builder)
    {
        builder.ToTable("PatchDeployment");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.ExternalReference).HasMaxLength(256);
        builder.Property(x => x.Summary).HasMaxLength(2000);
        builder.HasIndex(x => new { x.ConfigurationItemId, x.Status }).HasDatabaseName("IX_PatchDeployment_CI_Status");
        builder.HasIndex(x => x.ScheduledAtUtc).HasDatabaseName("IX_PatchDeployment_ScheduledAtUtc");
    }
}

internal sealed class ScheduledJobConfiguration : IEntityTypeConfiguration<ScheduledJob>
{
    public void Configure(EntityTypeBuilder<ScheduledJob> builder)
    {
        builder.ToTable("ScheduledJob");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Provider).HasMaxLength(64);
        builder.Property(x => x.ExternalJobId).HasMaxLength(128);
        builder.Property(x => x.ScheduleDescription).HasMaxLength(256);
        builder.Property(x => x.LastResult).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(x => x.NextRunAtUtc).HasDatabaseName("IX_ScheduledJob_NextRunAtUtc");
        builder.HasIndex(x => x.IsActive).HasDatabaseName("IX_ScheduledJob_IsActive");
    }
}
