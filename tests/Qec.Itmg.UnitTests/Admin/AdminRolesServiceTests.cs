using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Host.Persistence;
using Qec.Itmg.Identity.Admin;
using Qec.Itmg.Identity.Audit;
using Qec.Itmg.Identity.Domain;
using Qec.Itmg.Identity.Persistence;
using Qec.Itmg.Organization.Persistence;
using Qec.Itmg.Platform.Audit;
using Qec.Itmg.Platform.Persistence;
using Xunit;

namespace Qec.Itmg.UnitTests.Admin;

public sealed class AdminRolesServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 14, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateAndUpdate_Role()
    {
        await using AdminUsersServiceTests.AdminTestHost host = CreateHost();
        AdminRolesService service = CreateService(host);

        IResult created = await service.CreateAsync(
            new CreateAdminRoleRequest("Help Desk", "Queue agents"),
            CancellationToken.None);
        Assert.Equal(StatusCodes.Status201Created, GetStatusCode(created));

        Role role = await host.Identity.Roles.SingleAsync();
        string rowVersion = Convert.ToBase64String(role.RowVersion);

        IResult updated = await service.UpdateAsync(
            role.Id,
            new UpdateAdminRoleRequest("Help Desk Agent", "Updated", rowVersion),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, GetStatusCode(updated));
        await host.Identity.Entry(role).ReloadAsync();
        Assert.Equal("Help Desk Agent", role.Name);
        Assert.Equal("Updated", role.Description);
    }

    [Fact]
    public async Task Update_ConcurrencyConflict_Returns409()
    {
        await using AdminUsersServiceTests.AdminTestHost host = CreateHost();
        AdminRolesService service = CreateService(host);
        Role role = Role.Create("Ops", Now);
        host.Identity.Roles.Add(role);
        await host.Identity.SaveChangesAsync();

        IResult conflict = await service.UpdateAsync(
            role.Id,
            new UpdateAdminRoleRequest("Ops", "Nope", Convert.ToBase64String([1, 2, 3, 4])),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status409Conflict, GetStatusCode(conflict));
    }

    [Fact]
    public async Task ReplacePermissions_Validates_AndDeduplicates()
    {
        await using AdminUsersServiceTests.AdminTestHost host = CreateHost();
        AdminRolesService service = CreateService(host);
        Role role = Role.Create("Admin", Now);
        Permission users = Permission.Create("admin.users");
        Permission roles = Permission.Create("admin.roles");
        host.Identity.Roles.Add(role);
        host.Identity.Permissions.AddRange(users, roles);
        await host.Identity.SaveChangesAsync();

        IResult assigned = await service.ReplacePermissionsAsync(
            role.Id,
            new ReplaceRolePermissionsRequest([users.Id, users.Id, roles.Id]),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, GetStatusCode(assigned));
        Assert.Equal(2, await host.Identity.RolePermissions.CountAsync(link => link.RoleId == role.Id));

        IResult invalid = await service.ReplacePermissionsAsync(
            role.Id,
            new ReplaceRolePermissionsRequest([Guid.NewGuid()]),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, GetStatusCode(invalid));
    }

    [Fact]
    public async Task ListPermissions_ReturnsCatalog()
    {
        await using AdminUsersServiceTests.AdminTestHost host = CreateHost();
        AdminRolesService service = CreateService(host);
        host.Identity.Permissions.Add(Permission.Create("admin.users", "Manage users"));
        host.Identity.Permissions.Add(Permission.Create("admin.roles", "Manage roles"));
        await host.Identity.SaveChangesAsync();

        IReadOnlyList<AdminPermissionDto> permissions = await service.ListPermissionsAsync(CancellationToken.None);

        Assert.Equal(2, permissions.Count);
        Assert.Equal("admin.roles", permissions[0].Key);
        Assert.Equal("admin.users", permissions[1].Key);
    }

    private static AdminUsersServiceTests.AdminTestHost CreateHost() => AdminUsersServiceTests.AdminTestHost.Create();

    private static AdminRolesService CreateService(AdminUsersServiceTests.AdminTestHost host) =>
        new(host.Identity, host.Clock, host.BusinessAudit, host.SecurityAudit, host.Shared);

    private static int GetStatusCode(IResult result)
    {
        if (result is IStatusCodeHttpResult statusCodeResult)
        {
            return statusCodeResult.StatusCode ?? StatusCodes.Status200OK;
        }

        throw new InvalidOperationException($"Result type {result.GetType().Name} does not expose a status code.");
    }
}
