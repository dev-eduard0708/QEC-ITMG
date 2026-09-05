using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Qec.Itmg.AccessManagement.Persistence;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.ChangeManagement.Persistence;
using Qec.Itmg.Cmdb.Persistence;
using Qec.Itmg.DocumentManagement.Persistence;
using Qec.Itmg.Governance.Persistence;
using Qec.Itmg.Compliance.Persistence;
using Qec.Itmg.Evidence.Persistence;
using Qec.Itmg.Audit.Persistence;
using Qec.Itmg.Security.Persistence;
using Qec.Itmg.BusinessContinuity.Persistence;
using Qec.Itmg.ThirdParty.Persistence;
using Qec.Itmg.Reporting.Persistence;
using Qec.Itmg.Identity.Persistence;
using Qec.Itmg.Operations.Persistence;
using Qec.Itmg.Organization.Persistence;
using Qec.Itmg.Platform.Persistence;

namespace Qec.Itmg.Host.Persistence;

/// <summary>
/// Design-time factories for module DbContexts. Used by dotnet-ef with this Host as the startup project.
/// </summary>
public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>();
        optionsBuilder.UseSqlServer(
            DesignTimeConnectionString.Resolve(),
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", IdentityDbContext.SchemaName));
        return new IdentityDbContext(optionsBuilder.Options);
    }
}

public sealed class OrganizationDbContextFactory : IDesignTimeDbContextFactory<OrganizationDbContext>
{
    public OrganizationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<OrganizationDbContext>();
        optionsBuilder.UseSqlServer(
            DesignTimeConnectionString.Resolve(),
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", OrganizationDbContext.SchemaName));
        return new OrganizationDbContext(optionsBuilder.Options);
    }
}

public sealed class PlatformDbContextFactory : IDesignTimeDbContextFactory<PlatformDbContext>
{
    public PlatformDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PlatformDbContext>();
        optionsBuilder.UseSqlServer(
            DesignTimeConnectionString.Resolve(),
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", PlatformDbContext.SchemaName));
        return new PlatformDbContext(optionsBuilder.Options);
    }
}

public sealed class CmdbDbContextFactory : IDesignTimeDbContextFactory<CmdbDbContext>
{
    public CmdbDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CmdbDbContext>();
        optionsBuilder.UseSqlServer(
            DesignTimeConnectionString.Resolve(),
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", CmdbDbContext.SchemaName));
        return new CmdbDbContext(optionsBuilder.Options);
    }
}

public sealed class ChangeManagementDbContextFactory : IDesignTimeDbContextFactory<ChangeManagementDbContext>
{
    public ChangeManagementDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ChangeManagementDbContext>();
        optionsBuilder.UseSqlServer(
            DesignTimeConnectionString.Resolve(),
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", ChangeManagementDbContext.SchemaName));
        return new ChangeManagementDbContext(optionsBuilder.Options);
    }
}

public sealed class OperationsDbContextFactory : IDesignTimeDbContextFactory<OperationsDbContext>
{
    public OperationsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<OperationsDbContext>();
        optionsBuilder.UseSqlServer(
            DesignTimeConnectionString.Resolve(),
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", OperationsDbContext.SchemaName));
        return new OperationsDbContext(optionsBuilder.Options);
    }
}

public sealed class AccessManagementDbContextFactory : IDesignTimeDbContextFactory<AccessManagementDbContext>
{
    public AccessManagementDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AccessManagementDbContext>();
        optionsBuilder.UseSqlServer(
            DesignTimeConnectionString.Resolve(),
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", AccessManagementDbContext.SchemaName));
        return new AccessManagementDbContext(optionsBuilder.Options);
    }
}

public sealed class DocumentManagementDbContextFactory : IDesignTimeDbContextFactory<DocumentManagementDbContext>
{
    public DocumentManagementDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DocumentManagementDbContext>();
        optionsBuilder.UseSqlServer(
            DesignTimeConnectionString.Resolve(),
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", DocumentManagementDbContext.SchemaName));
        return new DocumentManagementDbContext(optionsBuilder.Options);
    }
}

public sealed class GovernanceDbContextFactory : IDesignTimeDbContextFactory<GovernanceDbContext>
{
    public GovernanceDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<GovernanceDbContext>();
        optionsBuilder.UseSqlServer(
            DesignTimeConnectionString.Resolve(),
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", GovernanceDbContext.SchemaName));
        return new GovernanceDbContext(optionsBuilder.Options);
    }
}

public sealed class ComplianceDbContextFactory : IDesignTimeDbContextFactory<ComplianceDbContext>
{
    public ComplianceDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ComplianceDbContext>();
        optionsBuilder.UseSqlServer(
            DesignTimeConnectionString.Resolve(),
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", ComplianceDbContext.SchemaName));
        return new ComplianceDbContext(optionsBuilder.Options);
    }
}

public sealed class EvidenceDbContextFactory : IDesignTimeDbContextFactory<EvidenceDbContext>
{
    public EvidenceDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<EvidenceDbContext>();
        optionsBuilder.UseSqlServer(
            DesignTimeConnectionString.Resolve(),
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", EvidenceDbContext.SchemaName));
        return new EvidenceDbContext(optionsBuilder.Options);
    }
}

public sealed class AuditDbContextFactory : IDesignTimeDbContextFactory<AuditDbContext>
{
    public AuditDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AuditDbContext>();
        optionsBuilder.UseSqlServer(
            DesignTimeConnectionString.Resolve(),
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", AuditDbContext.SchemaName));
        return new AuditDbContext(optionsBuilder.Options);
    }
}

public sealed class SecurityDbContextFactory : IDesignTimeDbContextFactory<SecurityDbContext>
{
    public SecurityDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SecurityDbContext>();
        optionsBuilder.UseSqlServer(
            DesignTimeConnectionString.Resolve(),
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", SecurityDbContext.SchemaName));
        return new SecurityDbContext(optionsBuilder.Options);
    }
}

public sealed class ContinuityDbContextFactory : IDesignTimeDbContextFactory<ContinuityDbContext>
{
    public ContinuityDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ContinuityDbContext>();
        optionsBuilder.UseSqlServer(
            DesignTimeConnectionString.Resolve(),
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", ContinuityDbContext.SchemaName));
        return new ContinuityDbContext(optionsBuilder.Options);
    }
}

public sealed class ThirdPartyDbContextFactory : IDesignTimeDbContextFactory<ThirdPartyDbContext>
{
    public ThirdPartyDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ThirdPartyDbContext>();
        optionsBuilder.UseSqlServer(
            DesignTimeConnectionString.Resolve(),
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", ThirdPartyDbContext.SchemaName));
        return new ThirdPartyDbContext(optionsBuilder.Options);
    }
}

public sealed class ReportingDbContextFactory : IDesignTimeDbContextFactory<ReportingDbContext>
{
    public ReportingDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ReportingDbContext>();
        optionsBuilder.UseSqlServer(
            DesignTimeConnectionString.Resolve(),
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", ReportingDbContext.SchemaName));
        return new ReportingDbContext(optionsBuilder.Options);
    }
}
