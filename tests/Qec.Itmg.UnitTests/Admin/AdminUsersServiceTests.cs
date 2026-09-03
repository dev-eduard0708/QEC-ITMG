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

public sealed class AdminUsersServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task List_SearchesByUpnAndDisplayName()
    {
        await using AdminTestHost host = AdminTestHost.Create();
        AdminUsersService service = host.CreateUsersService();

        host.Identity.Users.Add(User.Create("alice@qehc.edu.sa", "Alice Admin", UserType.Employee, Now));
        host.Identity.Users.Add(User.Create("bob@qehc.edu.sa", "Bob Agent", UserType.Employee, Now));
        await host.Identity.SaveChangesAsync();

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
        await using AdminTestHost host = AdminTestHost.Create();
        AdminUsersService service = host.CreateUsersService();

        IResult result = await service.CreateAsync(
            new CreateAdminUserRequest(
                "new@qehc.edu.sa",
                "New User",
                "Vendor",
                "Asia/Riyadh",
                "oid-new"),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status201Created, GetStatusCode(result));
        AdminUserDto? dto = await host.Identity.Users.AsNoTracking()
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
        await using AdminTestHost host = AdminTestHost.Create();
        AdminUsersService service = host.CreateUsersService();
        User user = User.Create("edit@qehc.edu.sa", "Edit Me", UserType.Employee, Now, directoryObjectId: "oid-edit");
        host.Identity.Users.Add(user);
        await host.Identity.SaveChangesAsync();

        string rowVersion = Convert.ToBase64String(user.RowVersion);
        IResult ok = await service.UpdateAsync(
            user.Id,
            new UpdateAdminUserRequest("Edited", "Service", "Disabled", "UTC", "oid-edit", rowVersion),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, GetStatusCode(ok));
        await host.Identity.Entry(user).ReloadAsync();
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
        await using AdminTestHost host = AdminTestHost.Create();
        AdminUsersService service = host.CreateUsersService();
        User user = User.Create("roles@qehc.edu.sa", "Roles User", UserType.Employee, Now);
        Role roleA = Role.Create("Role A", Now);
        Role roleB = Role.Create("Role B", Now);
        host.Identity.Users.Add(user);
        host.Identity.Roles.AddRange(roleA, roleB);
        await host.Identity.SaveChangesAsync();

        IResult assigned = await service.ReplaceRolesAsync(
            user.Id,
            new ReplaceUserRolesRequest([roleA.Id, roleA.Id, roleB.Id]),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, GetStatusCode(assigned));
        Assert.Equal(2, await host.Identity.UserRoles.CountAsync(userRole => userRole.UserId == user.Id));

        IResult invalid = await service.ReplaceRolesAsync(
            user.Id,
            new ReplaceUserRolesRequest([Guid.NewGuid()]),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, GetStatusCode(invalid));
    }

    private static int GetStatusCode(IResult result)
    {
        if (result is IStatusCodeHttpResult statusCodeResult)
        {
            return statusCodeResult.StatusCode ?? StatusCodes.Status200OK;
        }

        throw new InvalidOperationException($"Result type {result.GetType().Name} does not expose a status code.");
    }

    internal sealed class AdminTestHost : IAsyncDisposable
    {
        private AdminTestHost(
            IdentityDbContext identity,
            OrganizationDbContext organization,
            PlatformDbContext platform,
            FixedClock clock,
            IBusinessAuditWriter businessAudit,
            ISecurityAuditLogger securityAudit,
            ISharedDbTransaction shared)
        {
            Identity = identity;
            Organization = organization;
            Platform = platform;
            Clock = clock;
            BusinessAudit = businessAudit;
            SecurityAudit = securityAudit;
            Shared = shared;
        }

        public IdentityDbContext Identity { get; }
        public OrganizationDbContext Organization { get; }
        public PlatformDbContext Platform { get; }
        public FixedClock Clock { get; }
        public IBusinessAuditWriter BusinessAudit { get; }
        public ISecurityAuditLogger SecurityAudit { get; }
        public ISharedDbTransaction Shared { get; }

        public static AdminTestHost Create()
        {
            string name = Guid.NewGuid().ToString("N");
            IdentityDbContext identity = new(new DbContextOptionsBuilder<IdentityDbContext>()
                .UseInMemoryDatabase($"users-id-{name}")
                .Options);
            OrganizationDbContext organization = new(new DbContextOptionsBuilder<OrganizationDbContext>()
                .UseInMemoryDatabase($"users-org-{name}")
                .Options);
            PlatformDbContext platform = new(new DbContextOptionsBuilder<PlatformDbContext>()
                .UseInMemoryDatabase($"users-plt-{name}")
                .Options);
            FixedClock clock = new(Now);
            NullAuditRequestContext context = NullAuditRequestContext.Instance;
            IBusinessAuditWriter businessAudit = new EfBusinessAuditWriter(platform, clock, context);
            ISecurityAuditLogger securityAudit = new EfSecurityAuditLogger(platform, clock, context);
            ISharedDbTransaction shared = new SharedSqlTransaction(identity, organization, platform);
            return new AdminTestHost(identity, organization, platform, clock, businessAudit, securityAudit, shared);
        }

        public AdminUsersService CreateUsersService() =>
            new(Identity, Clock, BusinessAudit, SecurityAudit, Shared);

        public async ValueTask DisposeAsync()
        {
            await Identity.DisposeAsync();
            await Organization.DisposeAsync();
            await Platform.DisposeAsync();
        }
    }

    internal sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
