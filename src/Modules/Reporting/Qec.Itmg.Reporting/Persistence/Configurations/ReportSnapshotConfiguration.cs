using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qec.Itmg.Reporting.Domain;

namespace Qec.Itmg.Reporting.Persistence.Configurations;

internal sealed class ReportSnapshotConfiguration : IEntityTypeConfiguration<ReportSnapshot>
{
    public void Configure(EntityTypeBuilder<ReportSnapshot> builder)
    {
        builder.ToTable("ReportSnapshot");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SnapshotKey).IsRequired().HasMaxLength(128);
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.HasIndex(x => new { x.SnapshotKey, x.SnapshotDateUtc })
            .IsUnique()
            .HasDatabaseName("IX_ReportSnapshot_Key_Date");
        builder.HasIndex(x => x.SnapshotDateUtc).HasDatabaseName("IX_ReportSnapshot_Date");
        builder.HasIndex(x => x.PeriodStartUtc).HasDatabaseName("IX_ReportSnapshot_PeriodStart");
    }
}
