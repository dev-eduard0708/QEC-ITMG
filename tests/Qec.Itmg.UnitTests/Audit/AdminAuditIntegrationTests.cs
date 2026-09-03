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
using Qec.Itmg.Platform.Domain;
using Qec.Itmg.Platform.Persistence;
using Xunit;

namespace Qec.Itmg.UnitTests.Audit;

public sealed class AdminAuditIntegrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RoleAssignmentAndRemoval_CreateBusinessAndSecurityHistory()
    {
        await using AuditHarness harness = await AuditHarness.CreateAsync();
        User user = User.Create("target@qehc.edu.sa", "Target", UserType.Employee, Now);
        Role role = Role.Create("Help Desk", Now);
        harness.Identity.Users.Add(user);
        harness.Identity.Roles.Add(role);
        await harness.Identity.SaveChangesAsync();

        AdminUsersService users = harness.CreateUsersService();
        Assert.Equal(
            StatusCodes.Status200OK,
            GetStatusCode(await users.ReplaceRolesAsync(user.Id, new ReplaceUserRolesRequest([role.Id]), CancellationToken.None)));

        Assert.Contains(
            harness.Platform.BusinessAuditRecords.AsNoTracking(),
            record => record.Action == BusinessAuditAction.Assigned && record.AggregateId == user.Id);
        Assert.Contains(
            harness.Platform.SecurityAuditEvents.AsNoTracking(),
            record => record.EventType == SecurityEventType.RoleAssigned);

        Assert.Equal(
            StatusCodes.Status200OK,
            GetStatusCode(await users.ReplaceRolesAsync(user.Id, new ReplaceUserRolesRequest([]), CancellationToken.None)));

        Assert.Contains(
            harness.Platform.BusinessAuditRecords.AsNoTracking(),
            record => record.Action == BusinessAuditAction.Unassigned && record.AggregateId == user.Id);
        Assert.Contains(
            harness.Platform.SecurityAuditEvents.AsNoTracking(),
            record => record.EventType == SecurityEventType.RoleUnassigned);
    }

    [Fact]
    public async Task PermissionGrantAndRevoke_CreateBusinessAndSecurityAudit()
    {
        await using AuditHarness harness = await AuditHarness.CreateAsync();
        Role role = Role.Create("Ops", Now);
        Permission permission = Permission.Create("admin.users");
        harness.Identity.Roles.Add(role);
        harness.Identity.Permissions.Add(permission);
        await harness.Identity.SaveChangesAsync();

        AdminRolesService roles = harness.CreateRolesService();
        Assert.Equal(
            StatusCodes.Status200OK,
            GetStatusCode(await roles.ReplacePermissionsAsync(
                role.Id,
                new ReplaceRolePermissionsRequest([permission.Id]),
                CancellationToken.None)));

        Assert.Contains(
            harness.Platform.BusinessAuditRecords.AsNoTracking(),
            record => record.Action == BusinessAuditAction.Linked && record.FieldName == "Permission");
        Assert.Contains(
            harness.Platform.SecurityAuditEvents.AsNoTracking(),
            record => record.EventType == SecurityEventType.PermissionGranted);

        Assert.Equal(
            StatusCodes.Status200OK,
            GetStatusCode(await roles.ReplacePermissionsAsync(
                role.Id,
                new ReplaceRolePermissionsRequest([]),
                CancellationToken.None)));

        Assert.Contains(
            harness.Platform.BusinessAuditRecords.AsNoTracking(),
            record => record.Action == BusinessAuditAction.Unlinked);
        Assert.Contains(
            harness.Platform.SecurityAuditEvents.AsNoTracking(),
            record => record.EventType == SecurityEventType.PermissionRevoked);
    }

    [Fact]
    public async Task UserDisable_CreatesBusinessAndSecurityAudit_WithCorrelation()
    {
        await using AuditHarness harness = await AuditHarness.CreateAsync(
            correlationId: "corr-disable-1",
            clientIp: "10.0.0.8",
            actorUserId: Guid.CreateVersion7());

        User user = User.Create("disable@qehc.edu.sa", "Disable Me", UserType.Employee, Now);
        harness.Identity.Users.Add(user);
        await harness.Identity.SaveChangesAsync();

        AdminUsersService users = harness.CreateUsersService();
        IResult result = await users.UpdateAsync(
            user.Id,
            new UpdateAdminUserRequest(
                user.DisplayName,
                user.UserType.ToString(),
                "Disabled",
                user.TimeZone,
                user.DirectoryObjectId,
                Convert.ToBase64String(user.RowVersion)),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, GetStatusCode(result));

        BusinessAuditRecord status = await harness.Platform.BusinessAuditRecords.AsNoTracking()
            .SingleAsync(record => record.FieldName == "Status");
        Assert.Equal("Active", status.OldValue);
        Assert.Equal("Disabled", status.NewValue);
        Assert.Equal("corr-disable-1", status.CorrelationId);
        Assert.Equal("10.0.0.8", status.ClientIp);
        Assert.NotNull(status.ActorUserId);

        Assert.Contains(
            harness.Platform.SecurityAuditEvents.AsNoTracking(),
            record => record.EventType == SecurityEventType.UserDisabled && record.CorrelationId == "corr-disable-1");
    }

    [Fact]
    public async Task UnchangedFields_DoNotGenerateDiffs()
    {
        await using AuditHarness harness = await AuditHarness.CreateAsync();
        User user = User.Create("same@qehc.edu.sa", "Same", UserType.Employee, Now, timeZone: "UTC");
        harness.Identity.Users.Add(user);
        await harness.Identity.SaveChangesAsync();

        AdminUsersService users = harness.CreateUsersService();
        await users.UpdateAsync(
            user.Id,
            new UpdateAdminUserRequest(
                "Same",
                "Employee",
                "Active",
                "UTC",
                null,
                Convert.ToBase64String(user.RowVersion)),
            CancellationToken.None);

        Assert.Empty(await harness.Platform.BusinessAuditRecords.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task AuditWriteFailure_RollsBackBusinessMutation()
    {
        await using AuditHarness harness = await AuditHarness.CreateAsync(failingCommit: true);
        AdminUsersService users = harness.CreateUsersService();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await users.CreateAsync(
                new CreateAdminUserRequest("rollback@qehc.edu.sa", "Rollback", "Employee", null, null),
                CancellationToken.None));

        Assert.False(await harness.Identity.Users.AsNoTracking().AnyAsync(user => user.Upn == "rollback@qehc.edu.sa"));
        Assert.Empty(await harness.Platform.BusinessAuditRecords.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task AuditEntities_HaveNoUpdateApi_AndAreAppendOnlyInModel()
    {
        await using AuditHarness harness = await AuditHarness.CreateAsync();
        BusinessAuditRecord record = BusinessAuditRecord.Create(
            new BusinessAuditEntry
            {
                AggregateType = AuditAggregateType.User,
                AggregateId = Guid.CreateVersion7(),
                Action = BusinessAuditAction.Created,
                Source = AuditSource.Api,
            },
            Now,
            actorUserId: null,
            AuditActorType.System,
            jobName: null,
            correlationId: "c1",
            clientIp: null);

        harness.Platform.BusinessAuditRecords.Add(record);
        await harness.Platform.SaveChangesAsync();

        Assert.Null(typeof(BusinessAuditRecord).GetMethod("Update"));
        Assert.Null(typeof(SecurityAuditEvent).GetMethod("Update"));
        Assert.Null(typeof(BusinessAuditRecord).GetProperty("RowVersion"));
        Assert.DoesNotContain(
            harness.Platform.Model.FindEntityType(typeof(BusinessAuditRecord))!.GetProperties(),
            property => property.IsConcurrencyToken);
    }

    private static int GetStatusCode(IResult result)
    {
        if (result is IStatusCodeHttpResult statusCodeResult)
        {
            return statusCodeResult.StatusCode ?? StatusCodes.Status200OK;
        }

        throw new InvalidOperationException(result.GetType().Name);
    }

    private sealed class AuditHarness : IAsyncDisposable
    {
        private AuditHarness(
            IdentityDbContext identity,
            OrganizationDbContext organization,
            PlatformDbContext platform,
            IBusinessAuditWriter businessAudit,
            ISecurityAuditLogger securityAudit,
            ISharedDbTransaction sharedDbTransaction,
            FixedClock clock)
        {
            Identity = identity;
            Organization = organization;
            Platform = platform;
            BusinessAudit = businessAudit;
            SecurityAudit = securityAudit;
            SharedDbTransaction = sharedDbTransaction;
            Clock = clock;
        }

        public IdentityDbContext Identity { get; }
        public OrganizationDbContext Organization { get; }
        public PlatformDbContext Platform { get; }
        public IBusinessAuditWriter BusinessAudit { get; }
        public ISecurityAuditLogger SecurityAudit { get; }
        public ISharedDbTransaction SharedDbTransaction { get; }
        public FixedClock Clock { get; }

        public static Task<AuditHarness> CreateAsync(
            string? correlationId = "corr-test",
            string? clientIp = "127.0.0.1",
            Guid? actorUserId = null,
            bool failingCommit = false)
        {
            string name = Guid.NewGuid().ToString("N");
            IdentityDbContext identity = new(new DbContextOptionsBuilder<IdentityDbContext>()
                .UseInMemoryDatabase($"id-{name}")
                .Options);
            OrganizationDbContext organization = new(new DbContextOptionsBuilder<OrganizationDbContext>()
                .UseInMemoryDatabase($"org-{name}")
                .Options);
            PlatformDbContext platform = new(new DbContextOptionsBuilder<PlatformDbContext>()
                .UseInMemoryDatabase($"plt-{name}")
                .Options);

            FixedClock clock = new(Now);
            TestAuditRequestContext requestContext = new(actorUserId, correlationId, clientIp);
            IBusinessAuditWriter businessAudit = new EfBusinessAuditWriter(platform, clock, requestContext);
            ISecurityAuditLogger securityAudit = new EfSecurityAuditLogger(platform, clock, requestContext);
            ISharedDbTransaction shared = failingCommit
                ? new FailingSharedDbTransaction()
                : new SharedSqlTransaction(identity, organization, platform);

            return Task.FromResult(new AuditHarness(
                identity,
                organization,
                platform,
                businessAudit,
                securityAudit,
                shared,
                clock));
        }

        public AdminUsersService CreateUsersService() =>
            new(Identity, Clock, BusinessAudit, SecurityAudit, SharedDbTransaction);

        public AdminRolesService CreateRolesService() =>
            new(Identity, Clock, BusinessAudit, SecurityAudit, SharedDbTransaction);

        public async ValueTask DisposeAsync()
        {
            await Identity.DisposeAsync();
            await Organization.DisposeAsync();
            await Platform.DisposeAsync();
        }
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class TestAuditRequestContext(Guid? actorUserId, string? correlationId, string? clientIp)
        : IAuditRequestContext
    {
        public AuditActorType ActorType => actorUserId is null ? AuditActorType.System : AuditActorType.User;

        public string? JobName => null;

        public string? CorrelationId { get; } = correlationId;

        public string? ClientIp { get; } = clientIp;

        public Task<Guid?> GetActorUserIdAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(actorUserId);
    }

    /// <summary>
    /// Simulates audit persistence failure after staging business + audit changes, before commit.
    /// </summary>
    private sealed class FailingSharedDbTransaction : ISharedDbTransaction
    {
        public async Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default)
        {
            await work(cancellationToken);
            throw new InvalidOperationException("Simulated audit persistence failure.");
        }
    }
}
