using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Identity.Admin;
using Qec.Itmg.Identity.Domain;
using Qec.Itmg.Identity.Persistence;
using Xunit;

namespace Qec.Itmg.UnitTests.Admin;

public sealed class AdminRolesServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 14, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateAndUpdate_Role()
    {
        await using IdentityDbContext db = CreateDb();
        AdminRolesService service = CreateService(db);

        IResult created = await service.CreateAsync(
            new CreateAdminRoleRequest("Help Desk", "Queue agents"),
            CancellationToken.None);
        Assert.Equal(StatusCodes.Status201Created, GetStatusCode(created));

        Role role = await db.Roles.SingleAsync();
        string rowVersion = Convert.ToBase64String(role.RowVersion);

        IResult updated = await service.UpdateAsync(
            role.Id,
            new UpdateAdminRoleRequest("Help Desk Agent", "Updated", rowVersion),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, GetStatusCode(updated));
        await db.Entry(role).ReloadAsync();
        Assert.Equal("Help Desk Agent", role.Name);
        Assert.Equal("Updated", role.Description);
    }

    [Fact]
    public async Task Update_ConcurrencyConflict_Returns409()
    {
        await using IdentityDbContext db = CreateDb();
        AdminRolesService service = CreateService(db);
        Role role = Role.Create("Ops", Now);
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        IResult conflict = await service.UpdateAsync(
            role.Id,
            new UpdateAdminRoleRequest("Ops", "Nope", Convert.ToBase64String([1, 2, 3, 4])),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status409Conflict, GetStatusCode(conflict));
    }

    [Fact]
    public async Task ReplacePermissions_Validates_AndDeduplicates()
    {
        await using IdentityDbContext db = CreateDb();
        AdminRolesService service = CreateService(db);
        Role role = Role.Create("Admin", Now);
        Permission users = Permission.Create("admin.users");
        Permission roles = Permission.Create("admin.roles");
        db.Roles.Add(role);
        db.Permissions.AddRange(users, roles);
        await db.SaveChangesAsync();

        IResult assigned = await service.ReplacePermissionsAsync(
            role.Id,
            new ReplaceRolePermissionsRequest([users.Id, users.Id, roles.Id]),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, GetStatusCode(assigned));
        Assert.Equal(2, await db.RolePermissions.CountAsync(link => link.RoleId == role.Id));

        IResult invalid = await service.ReplacePermissionsAsync(
            role.Id,
            new ReplaceRolePermissionsRequest([Guid.NewGuid()]),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, GetStatusCode(invalid));
    }

    [Fact]
    public async Task ListPermissions_ReturnsCatalog()
    {
        await using IdentityDbContext db = CreateDb();
        AdminRolesService service = CreateService(db);
        db.Permissions.Add(Permission.Create("admin.users", "Manage users"));
        db.Permissions.Add(Permission.Create("admin.roles", "Manage roles"));
        await db.SaveChangesAsync();

        IReadOnlyList<AdminPermissionDto> permissions = await service.ListPermissionsAsync(CancellationToken.None);

        Assert.Equal(2, permissions.Count);
        Assert.Equal("admin.roles", permissions[0].Key);
        Assert.Equal("admin.users", permissions[1].Key);
    }

    private static AdminRolesService CreateService(IdentityDbContext db) =>
        new(db, new FixedClock(Now));

    private static IdentityDbContext CreateDb()
    {
        DbContextOptions<IdentityDbContext> options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new IdentityDbContext(options);
    }

    private static int GetStatusCode(IResult result)
    {
        if (result is IStatusCodeHttpResult statusCodeResult)
        {
            return statusCodeResult.StatusCode ?? StatusCodes.Status200OK;
        }

        throw new InvalidOperationException($"Result type {result.GetType().Name} does not expose a status code.");
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
