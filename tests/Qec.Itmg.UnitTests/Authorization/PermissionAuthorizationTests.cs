using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Qec.Itmg.Identity.Authentication;
using Qec.Itmg.Identity.Authorization;
using Qec.Itmg.Identity.Domain;
using Qec.Itmg.Identity.Persistence;
using Xunit;

namespace Qec.Itmg.UnitTests.Authorization;

public sealed class PermissionAuthorizationTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task User_WithSqlPermission_Succeeds()
    {
        await using IdentityDbContext db = CreateDb();
        User user = await SeedUserWithPermissionAsync(db, "admin.users", UserStatus.Active);

        ClaimsPrincipal principal = CreatePrincipal(externalId: user.DirectoryObjectId!, upn: user.Upn);
        IUserPermissionEvaluator evaluator = new SqlUserPermissionEvaluator(db);

        Assert.True(await evaluator.HasPermissionAsync(principal, "admin.users"));
        Assert.True(await AuthorizeAsync(db, principal, "admin.users"));
    }

    [Fact]
    public async Task AuthenticatedUser_WithoutPermission_Fails()
    {
        await using IdentityDbContext db = CreateDb();
        User user = await SeedUserWithPermissionAsync(db, "admin.roles", UserStatus.Active);

        ClaimsPrincipal principal = CreatePrincipal(externalId: user.DirectoryObjectId!, upn: user.Upn);
        Assert.False(await new SqlUserPermissionEvaluator(db).HasPermissionAsync(principal, "admin.users"));
        Assert.False(await AuthorizeAsync(db, principal, "admin.users"));
    }

    [Fact]
    public async Task DisabledUser_Fails()
    {
        await using IdentityDbContext db = CreateDb();
        User user = await SeedUserWithPermissionAsync(db, "admin.users", UserStatus.Disabled);

        ClaimsPrincipal principal = CreatePrincipal(externalId: user.DirectoryObjectId!, upn: user.Upn);
        Assert.False(await new SqlUserPermissionEvaluator(db).HasPermissionAsync(principal, "admin.users"));
    }

    [Fact]
    public async Task UnknownLocalUser_Fails()
    {
        await using IdentityDbContext db = CreateDb();
        ClaimsPrincipal principal = CreatePrincipal(externalId: "missing-oid", upn: "missing@qehc.edu.sa");

        Assert.False(await new SqlUserPermissionEvaluator(db).HasPermissionAsync(principal, "admin.users"));
    }

    [Fact]
    public async Task RoleNameAlone_GrantsNothing()
    {
        await using IdentityDbContext db = CreateDb();
        User user = User.Create("roleonly@qehc.edu.sa", "Role Only", UserType.Employee, Now, directoryObjectId: "oid-role-only");
        Role role = Role.Create("Platform Administrator", Now, isSystem: true);
        db.Users.Add(user);
        db.Roles.Add(role);
        db.UserRoles.Add(UserRole.Create(user.Id, role.Id, Now));
        await db.SaveChangesAsync();

        ClaimsPrincipal principal = CreatePrincipal(externalId: user.DirectoryObjectId!, upn: user.Upn);
        Assert.False(await new SqlUserPermissionEvaluator(db).HasPermissionAsync(principal, "admin.users"));
    }

    [Fact]
    public async Task IdpRolesAndGroups_GrantNothing()
    {
        await using IdentityDbContext db = CreateDb();
        User user = await SeedUserWithPermissionAsync(db, "admin.users", UserStatus.Active);

        ClaimsIdentity identity = new("oidc");
        identity.AddClaim(new Claim(OidcPrincipalMapper.ExternalIdClaimType, user.DirectoryObjectId!));
        identity.AddClaim(new Claim(OidcPrincipalMapper.UpnClaimType, user.Upn));
        identity.AddClaim(new Claim(ClaimTypes.Role, "Domain Admins"));
        identity.AddClaim(new Claim("groups", "group-1"));
        ClaimsPrincipal principal = new(identity);

        Assert.False(await new SqlUserPermissionEvaluator(db).HasPermissionAsync(principal, "admin.users"));
    }

    [Fact]
    public async Task UpnFallback_ResolvesActivePreProvisionedUser()
    {
        await using IdentityDbContext db = CreateDb();
        User user = await SeedUserWithPermissionAsync(
            db,
            "admin.settings",
            UserStatus.Active,
            directoryObjectId: null);

        ClaimsPrincipal principal = CreatePrincipal(externalId: "unmapped-oid", upn: user.Upn);
        Assert.True(await new SqlUserPermissionEvaluator(db).HasPermissionAsync(principal, "admin.settings"));
    }

    [Fact]
    public async Task DynamicPermissionPolicy_IsCreated()
    {
        PermissionAuthorizationPolicyProvider provider = new(Options.Create(new AuthorizationOptions()));
        AuthorizationPolicy? policy = await provider.GetPolicyAsync("admin.users");

        Assert.NotNull(policy);
        Assert.Contains(policy!.Requirements, requirement => requirement is PermissionRequirement);
        Assert.Contains(
            policy.Requirements.OfType<PermissionRequirement>(),
            requirement => requirement.PermissionKey == "admin.users");
    }

    [Fact]
    public async Task InvalidPermissionPolicy_IsRejected()
    {
        PermissionAuthorizationPolicyProvider provider = new(Options.Create(new AuthorizationOptions()));

        Assert.Null(await provider.GetPolicyAsync("NotAPermission"));
        Assert.Null(await provider.GetPolicyAsync("admin"));
        Assert.Throws<ArgumentException>(() => PermissionPolicyName.For("Invalid Key"));
    }

    private static async Task<bool> AuthorizeAsync(
        IdentityDbContext db,
        ClaimsPrincipal principal,
        string permissionKey)
    {
        PermissionAuthorizationHandler handler = new(new SqlUserPermissionEvaluator(db));
        PermissionRequirement requirement = new(permissionKey);
        AuthorizationHandlerContext context = new([requirement], principal, resource: null);
        await handler.HandleAsync(context);
        return context.HasSucceeded;
    }

    private static IdentityDbContext CreateDb()
    {
        DbContextOptions<IdentityDbContext> options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new IdentityDbContext(options);
    }

    private static async Task<User> SeedUserWithPermissionAsync(
        IdentityDbContext db,
        string permissionKey,
        UserStatus status,
        string? directoryObjectId = "oid-seed")
    {
        User user = User.Create(
            $"{Guid.NewGuid():N}@qehc.edu.sa",
            "Seed User",
            UserType.Employee,
            Now,
            directoryObjectId: directoryObjectId);
        if (status == UserStatus.Disabled)
        {
            user.Disable(Now);
        }

        Role role = Role.Create($"role-{Guid.NewGuid():N}"[..20], Now);
        Permission permission = Permission.Create(permissionKey);
        db.Users.Add(user);
        db.Roles.Add(role);
        db.Permissions.Add(permission);
        db.UserRoles.Add(UserRole.Create(user.Id, role.Id, Now));
        db.RolePermissions.Add(RolePermission.Create(role.Id, permission.Id));
        await db.SaveChangesAsync();
        return user;
    }

    private static ClaimsPrincipal CreatePrincipal(string externalId, string upn)
    {
        ClaimsIdentity identity = new(
            [
                new Claim(OidcPrincipalMapper.ExternalIdClaimType, externalId),
                new Claim(ClaimTypes.NameIdentifier, externalId),
                new Claim(OidcPrincipalMapper.UpnClaimType, upn),
                new Claim(ClaimTypes.Upn, upn),
            ],
            authenticationType: "Test");
        return new ClaimsPrincipal(identity);
    }
}
