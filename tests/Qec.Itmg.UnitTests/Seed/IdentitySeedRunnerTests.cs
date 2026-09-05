using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Host.Persistence;
using Qec.Itmg.Identity.Audit;
using Qec.Itmg.Identity.Domain;
using Qec.Itmg.Identity.Persistence;
using Qec.Itmg.Identity.Seed;
using Qec.Itmg.Organization.Persistence;
using Qec.Itmg.Platform.Audit;
using Qec.Itmg.Platform.Persistence;
using Xunit;

namespace Qec.Itmg.UnitTests.Seed;

public sealed class IdentitySeedRunnerTests
{
    [Fact]
    public async Task Seed_IsIdempotent_AndEmployeeHasNoAdminPermissions()
    {
        await using ServiceProvider provider = CreateProvider(platformAdminUpn: null);
        using (IServiceScope scope = provider.CreateScope())
        {
            IIdentitySeedRunner runner = scope.ServiceProvider.GetRequiredService<IIdentitySeedRunner>();
            await runner.RunAsync();
            await runner.RunAsync();
        }

        using IServiceScope verify = provider.CreateScope();
        IdentityDbContext db = verify.ServiceProvider.GetRequiredService<IdentityDbContext>();
        string[] expectedKeys = IdentitySeedCatalog.SystemPermissions.Select(item => item.Key).ToArray();

        Assert.Equal(
            expectedKeys.Length,
            await db.Permissions.CountAsync(permission => expectedKeys.Contains(permission.Key)));

        Assert.Equal(1, await db.Roles.CountAsync(role => role.Name == IdentitySeedCatalog.EmployeeRoleName));
        Assert.Equal(
            1,
            await db.Roles.CountAsync(role => role.Name == IdentitySeedCatalog.PlatformAdministratorRoleName));

        Role employee = await db.Roles.SingleAsync(role => role.Name == IdentitySeedCatalog.EmployeeRoleName);
        Assert.False(await db.RolePermissions.AnyAsync(link => link.RoleId == employee.Id));

        Role platformAdmin = await db.Roles.SingleAsync(
            role => role.Name == IdentitySeedCatalog.PlatformAdministratorRoleName);
        List<string> keys = await db.RolePermissions
            .Where(link => link.RoleId == platformAdmin.Id)
            .Select(link => link.Permission.Key)
            .OrderBy(key => key)
            .ToListAsync();

        Assert.Equal(expectedKeys.OrderBy(key => key), keys);
        Assert.Contains(keys, key => key == "remote.unattended");
        Assert.Contains(keys, key => key == "remote.request");
    }

    [Fact]
    public async Task Seed_BootstrapUpn_AssignsPlatformAdministrator()
    {
        const string upn = "bootstrap-admin@example.test";
        await using ServiceProvider provider = CreateProvider(platformAdminUpn: upn);
        using (IServiceScope scope = provider.CreateScope())
        {
            IIdentitySeedRunner runner = scope.ServiceProvider.GetRequiredService<IIdentitySeedRunner>();
            await runner.RunAsync();
            await runner.RunAsync();
        }

        using IServiceScope verify = provider.CreateScope();
        IdentityDbContext db = verify.ServiceProvider.GetRequiredService<IdentityDbContext>();
        User user = await db.Users.SingleAsync(candidate => candidate.Upn == upn);
        Assert.Equal(UserStatus.Active, user.Status);

        Role platformAdmin = await db.Roles.SingleAsync(
            role => role.Name == IdentitySeedCatalog.PlatformAdministratorRoleName);
        Assert.Equal(
            1,
            await db.UserRoles.CountAsync(link => link.UserId == user.Id && link.RoleId == platformAdmin.Id));
    }

    private static ServiceProvider CreateProvider(string? platformAdminUpn)
    {
        string databaseName = Guid.NewGuid().ToString("N");
        ServiceCollection services = new();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton(Options.Create(new IdentitySeedOptions
        {
            PlatformAdministratorUpn = platformAdminUpn,
        }));
        services.AddHttpContextAccessor();
        services.AddScoped<IAuditRequestContext, IdentityAuditRequestContext>();
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseInMemoryDatabase($"seed-identity-{databaseName}"));
        services.AddDbContext<OrganizationDbContext>(options =>
            options.UseInMemoryDatabase($"seed-organization-{databaseName}"));
        services.AddDbContext<PlatformDbContext>(options =>
            options.UseInMemoryDatabase($"seed-platform-{databaseName}"));
        services.AddScoped<IBusinessAuditWriter, EfBusinessAuditWriter>();
        services.AddScoped<ISharedDbTransaction, SharedSqlTransaction>();
        services.AddScoped<IIdentitySeedRunner, IdentitySeedRunner>();
        services.AddLogging();
        return services.BuildServiceProvider();
    }
}
