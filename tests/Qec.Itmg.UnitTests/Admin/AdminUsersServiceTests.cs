using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Identity.Admin;
using Qec.Itmg.Identity.Domain;
using Qec.Itmg.Identity.Persistence;
using Xunit;

namespace Qec.Itmg.UnitTests.Admin;

public sealed class AdminUsersServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task List_SearchesByUpnAndDisplayName()
    {
        await using IdentityDbContext db = CreateDb();
        AdminUsersService service = CreateService(db);

        db.Users.Add(User.Create("alice@qehc.edu.sa", "Alice Admin", UserType.Employee, Now));
        db.Users.Add(User.Create("bob@qehc.edu.sa", "Bob Agent", UserType.Employee, Now));
        await db.SaveChangesAsync();

        IReadOnlyList<AdminUserDto> byUpn = await service.ListAsync("alice@", CancellationToken.None);
        IReadOnlyList<AdminUserDto> byName = await service.ListAsync("Agent", CancellationToken.None);

        Assert.Single(byUpn);
        Assert.Equal("alice@qehc.edu.sa", byUpn[0].Upn);
        Assert.Single(byName);
        Assert.Equal("bob@qehc.edu.sa", byName[0].Upn);
    }

    [Fact]
    public async Task Create_PreProvisionsUser()
    {
        await using IdentityDbContext db = CreateDb();
        AdminUsersService service = CreateService(db);

        IResult result = await service.CreateAsync(
            new CreateAdminUserRequest(
                "new@qehc.edu.sa",
                "New User",
                "Vendor",
                "Asia/Riyadh",
                "oid-new"),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status201Created, GetStatusCode(result));
        AdminUserDto? dto = await db.Users.AsNoTracking()
            .Where(user => user.Upn == "new@qehc.edu.sa")
            .Select(user => new AdminUserDto(
                user.Id,
                user.Upn,
                user.DisplayName,
                user.Status.ToString(),
                user.UserType.ToString(),
                user.DirectoryObjectId,
                user.TimeZone,
                Convert.ToBase64String(user.RowVersion),
                Array.Empty<AdminRoleSummaryDto>()))
            .SingleOrDefaultAsync();

        Assert.NotNull(dto);
        Assert.Equal("Vendor", dto!.UserType);
        Assert.Equal("Asia/Riyadh", dto.TimeZone);
        Assert.Equal("oid-new", dto.DirectoryObjectId);
    }

    [Fact]
    public async Task Update_AppliesProfileAndStatus_WithConcurrency()
    {
        await using IdentityDbContext db = CreateDb();
        AdminUsersService service = CreateService(db);
        User user = User.Create("edit@qehc.edu.sa", "Edit Me", UserType.Employee, Now, directoryObjectId: "oid-edit");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        string rowVersion = Convert.ToBase64String(user.RowVersion);
        IResult ok = await service.UpdateAsync(
            user.Id,
            new UpdateAdminUserRequest("Edited", "Service", "Disabled", "UTC", "oid-edit", rowVersion),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, GetStatusCode(ok));
        await db.Entry(user).ReloadAsync();
        Assert.Equal("Edited", user.DisplayName);
        Assert.Equal(UserType.Service, user.UserType);
        Assert.Equal(UserStatus.Disabled, user.Status);

        IResult conflict = await service.UpdateAsync(
            user.Id,
            new UpdateAdminUserRequest(
                "Stale",
                "Employee",
                "Active",
                null,
                null,
                Convert.ToBase64String([9, 9, 9, 9])),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status409Conflict, GetStatusCode(conflict));
    }

    [Fact]
    public async Task ReplaceRoles_Validates_AndPreventsDuplicates()
    {
        await using IdentityDbContext db = CreateDb();
        AdminUsersService service = CreateService(db);
        User user = User.Create("roles@qehc.edu.sa", "Roles User", UserType.Employee, Now);
        Role roleA = Role.Create("Role A", Now);
        Role roleB = Role.Create("Role B", Now);
        db.Users.Add(user);
        db.Roles.AddRange(roleA, roleB);
        await db.SaveChangesAsync();

        IResult assigned = await service.ReplaceRolesAsync(
            user.Id,
            new ReplaceUserRolesRequest([roleA.Id, roleA.Id, roleB.Id]),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, GetStatusCode(assigned));
        Assert.Equal(2, await db.UserRoles.CountAsync(userRole => userRole.UserId == user.Id));

        IResult invalid = await service.ReplaceRolesAsync(
            user.Id,
            new ReplaceUserRolesRequest([Guid.NewGuid()]),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, GetStatusCode(invalid));
    }

    private static AdminUsersService CreateService(IdentityDbContext db) =>
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
