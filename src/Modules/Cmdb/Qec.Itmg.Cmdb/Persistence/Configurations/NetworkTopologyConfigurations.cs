using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qec.Itmg.Cmdb.Domain;

namespace Qec.Itmg.Cmdb.Persistence.Configurations;

internal sealed class CiNetworkIdentityConfiguration : IEntityTypeConfiguration<CiNetworkIdentity>
{
    public void Configure(EntityTypeBuilder<CiNetworkIdentity> builder)
    {
        builder.ToTable("CiNetworkIdentity");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.IpAddress)
            .IsRequired()
            .HasMaxLength(64)
            .HasColumnType("nvarchar(64)");

        builder.Property(item => item.Hostname)
            .HasMaxLength(255)
            .HasColumnType("nvarchar(255)");

        builder.Property(item => item.MacAddress)
            .HasMaxLength(64)
            .HasColumnType("nvarchar(64)");

        builder.Property(item => item.IsPrimary).IsRequired();
        builder.Property(item => item.CreatedAtUtc).IsRequired();
        builder.Property(item => item.UpdatedAtUtc).IsRequired();

        builder.HasIndex(item => item.ConfigurationItemId)
            .HasDatabaseName("IX_CiNetworkIdentity_ConfigurationItemId");

        builder.HasIndex(item => item.IpAddress)
            .HasDatabaseName("IX_CiNetworkIdentity_IpAddress");

        builder.HasIndex(item => new { item.ConfigurationItemId, item.IpAddress })
            .IsUnique()
            .HasDatabaseName("IX_CiNetworkIdentity_Ci_Ip");

        builder.HasOne<ConfigurationItem>()
            .WithMany()
            .HasForeignKey(item => item.ConfigurationItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class NetworkTopologyViewConfiguration : IEntityTypeConfiguration<NetworkTopologyView>
{
    public void Configure(EntityTypeBuilder<NetworkTopologyView> builder)
    {
        builder.ToTable("NetworkTopologyView");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.Name)
            .IsRequired()
            .HasMaxLength(128)
            .HasColumnType("nvarchar(128)");

        builder.Property(item => item.Description)
            .HasMaxLength(1024)
            .HasColumnType("nvarchar(1024)");

        builder.Property(item => item.IsDefault).IsRequired();
        builder.Property(item => item.CreatedAtUtc).IsRequired();
        builder.Property(item => item.UpdatedAtUtc).IsRequired();
        builder.Property(item => item.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasIndex(item => item.Name)
            .HasDatabaseName("IX_NetworkTopologyView_Name");
    }
}

internal sealed class NetworkTopologyNodeLayoutConfiguration : IEntityTypeConfiguration<NetworkTopologyNodeLayout>
{
    public void Configure(EntityTypeBuilder<NetworkTopologyNodeLayout> builder)
    {
        builder.ToTable("NetworkTopologyNodeLayout");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.PositionX).IsRequired();
        builder.Property(item => item.PositionY).IsRequired();
        builder.Property(item => item.UpdatedAtUtc).IsRequired();

        builder.HasIndex(item => new { item.TopologyViewId, item.ConfigurationItemId })
            .IsUnique()
            .HasDatabaseName("IX_NetworkTopologyNodeLayout_View_Ci");

        builder.HasOne<NetworkTopologyView>()
            .WithMany()
            .HasForeignKey(item => item.TopologyViewId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ConfigurationItem>()
            .WithMany()
            .HasForeignKey(item => item.ConfigurationItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class NetworkLinkDetailConfiguration : IEntityTypeConfiguration<NetworkLinkDetail>
{
    public void Configure(EntityTypeBuilder<NetworkLinkDetail> builder)
    {
        builder.ToTable("NetworkLinkDetail");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.FromPort)
            .HasMaxLength(64)
            .HasColumnType("nvarchar(64)");

        builder.Property(item => item.ToPort)
            .HasMaxLength(64)
            .HasColumnType("nvarchar(64)");

        builder.Property(item => item.MediaType)
            .HasMaxLength(32)
            .HasColumnType("nvarchar(32)");

        builder.Property(item => item.LinkMode)
            .HasMaxLength(32)
            .HasColumnType("nvarchar(32)");

        builder.Property(item => item.Notes)
            .HasMaxLength(1024)
            .HasColumnType("nvarchar(1024)");

        builder.Property(item => item.IsConfirmed).IsRequired();
        builder.Property(item => item.UpdatedAtUtc).IsRequired();

        builder.HasIndex(item => item.RelationshipId)
            .IsUnique()
            .HasDatabaseName("IX_NetworkLinkDetail_RelationshipId");

        builder.HasOne<CiRelationship>()
            .WithMany()
            .HasForeignKey(item => item.RelationshipId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class NetworkDiscoveryProfileConfiguration : IEntityTypeConfiguration<NetworkDiscoveryProfile>
{
    public void Configure(EntityTypeBuilder<NetworkDiscoveryProfile> builder)
    {
        builder.ToTable("NetworkDiscoveryProfile");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.Name)
            .IsRequired()
            .HasMaxLength(128)
            .HasColumnType("nvarchar(128)");

        builder.Property(item => item.Cidr)
            .IsRequired()
            .HasMaxLength(64)
            .HasColumnType("nvarchar(64)");

        builder.Property(item => item.IsActive).IsRequired();
        builder.Property(item => item.TimeoutMs).IsRequired();
        builder.Property(item => item.MaxConcurrency).IsRequired();
        builder.Property(item => item.CreatedAtUtc).IsRequired();
        builder.Property(item => item.UpdatedAtUtc).IsRequired();

        builder.HasIndex(item => item.Name)
            .HasDatabaseName("IX_NetworkDiscoveryProfile_Name");
    }
}

internal sealed class NetworkDiscoveryRunConfiguration : IEntityTypeConfiguration<NetworkDiscoveryRun>
{
    public void Configure(EntityTypeBuilder<NetworkDiscoveryRun> builder)
    {
        builder.ToTable("NetworkDiscoveryRun");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasColumnType("nvarchar(32)");

        builder.Property(item => item.StartedAtUtc).IsRequired();
        builder.Property(item => item.AddressesScanned).IsRequired();
        builder.Property(item => item.ResponsiveHosts).IsRequired();
        builder.Property(item => item.ErrorSummary)
            .HasMaxLength(2000)
            .HasColumnType("nvarchar(2000)");

        builder.HasIndex(item => item.ProfileId)
            .HasDatabaseName("IX_NetworkDiscoveryRun_ProfileId");

        builder.HasIndex(item => item.StartedAtUtc)
            .HasDatabaseName("IX_NetworkDiscoveryRun_StartedAtUtc");

        builder.HasOne<NetworkDiscoveryProfile>()
            .WithMany()
            .HasForeignKey(item => item.ProfileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class NetworkDiscoveryObservationConfiguration : IEntityTypeConfiguration<NetworkDiscoveryObservation>
{
    public void Configure(EntityTypeBuilder<NetworkDiscoveryObservation> builder)
    {
        builder.ToTable("NetworkDiscoveryObservation");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.IpAddress)
            .IsRequired()
            .HasMaxLength(64)
            .HasColumnType("nvarchar(64)");

        builder.Property(item => item.Hostname)
            .HasMaxLength(255)
            .HasColumnType("nvarchar(255)");

        builder.Property(item => item.MacAddress)
            .HasMaxLength(64)
            .HasColumnType("nvarchar(64)");

        builder.Property(item => item.Vendor)
            .HasMaxLength(128)
            .HasColumnType("nvarchar(128)");

        builder.Property(item => item.MatchStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasColumnType("nvarchar(32)");

        builder.Property(item => item.ReviewStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasColumnType("nvarchar(32)");

        builder.Property(item => item.ObservedAtUtc).IsRequired();

        builder.HasIndex(item => item.RunId)
            .HasDatabaseName("IX_NetworkDiscoveryObservation_RunId");

        builder.HasIndex(item => new { item.RunId, item.IpAddress })
            .IsUnique()
            .HasDatabaseName("IX_NetworkDiscoveryObservation_Run_Ip");

        builder.HasOne<NetworkDiscoveryRun>()
            .WithMany()
            .HasForeignKey(item => item.RunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ConfigurationItem>()
            .WithMany()
            .HasForeignKey(item => item.MatchedConfigurationItemId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
