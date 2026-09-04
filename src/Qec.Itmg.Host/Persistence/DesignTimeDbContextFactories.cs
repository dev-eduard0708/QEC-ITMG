using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Qec.Itmg.AccessManagement.Persistence;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.ChangeManagement.Persistence;
using Qec.Itmg.Cmdb.Persistence;
using Qec.Itmg.DocumentManagement.Persistence;
using Qec.Itmg.Governance.Persistence;
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
