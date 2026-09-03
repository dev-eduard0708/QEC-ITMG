using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Qec.Itmg.Identity.Authentication;
using Qec.Itmg.Identity.Authorization;
using Qec.Itmg.Identity.Domain;
using Qec.Itmg.Identity.Persistence;
using Qec.Itmg.Cmdb.Persistence;
using Qec.Itmg.ServiceDesk.Persistence;
using Qec.Itmg.Organization.Persistence;
using Qec.Itmg.Platform.Persistence;
using Xunit;

namespace Qec.Itmg.UnitTests.Authentication;

[Collection(WebApplicationFactoryCollection.Name)]
public sealed class BreakGlassAuthenticationTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 15, 0, 0, TimeSpan.Zero);
    private const string Username = "breakglass.ops";
    private const string Password = "Unit-Test-Only-Not-A-Secret!";
    private const string MappedUpn = "breakglass@qehc.edu.sa";

    [Fact]
    public async Task BreakGlass_WhenDisabled_IsRejected()
    {
        await using BreakGlassWebApplicationFactory factory = new(enabled: false);
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/auth/break-glass",
            new { username = Username, password = Password });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task BreakGlass_BadCredentials_Rejected()
    {
        await using BreakGlassWebApplicationFactory factory = new(enabled: true);
        await SeedMappedUserAsync(factory, UserStatus.Active);
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
        });

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/auth/break-glass",
            new { username = Username, password = "wrong-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        HttpResponseMessage state = await client.GetAsync("/__test__/auth-state");
        Assert.Equal("anonymous", await state.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task BreakGlass_ValidAccount_SignsInWithBreakGlassClaim()
    {
        await using BreakGlassWebApplicationFactory factory = new(enabled: true);
        await SeedMappedUserAsync(factory, UserStatus.Active);
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
        });

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/auth/break-glass",
            new { username = Username, password = Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            BreakGlassPrincipalFactory.AuthMethodBreakGlass,
            body.RootElement.GetProperty("authMethod").GetString());

        HttpResponseMessage state = await client.GetAsync("/__test__/auth-state");
        Assert.Equal("authenticated", await state.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task BreakGlass_MappedDisabledUser_Rejected()
    {
        await using BreakGlassWebApplicationFactory factory = new(enabled: true);
        await SeedMappedUserAsync(factory, UserStatus.Disabled);
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
        });

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/auth/break-glass",
            new { username = Username, password = Password });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        HttpResponseMessage state = await client.GetAsync("/__test__/auth-state");
        Assert.Equal("anonymous", await state.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task BreakGlass_Principal_DoesNotBypassSqlRbac()
    {
        await using IdentityDbContext db = CreateIdentityDb();
        User user = User.Create(MappedUpn, "Break Glass", UserType.Employee, Now, directoryObjectId: "bg-dir-1");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        ClaimsPrincipal principal = BreakGlassPrincipalFactory.Create(user);
        Assert.True(BreakGlassPrincipalFactory.IsBreakGlass(principal));
        Assert.False(OidcPrincipalMapper.ContainsAuthorizationRoleClaims(principal));

        IUserPermissionEvaluator evaluator = new SqlUserPermissionEvaluator(db);
        Assert.False(await evaluator.HasPermissionAsync(principal, "admin.users"));

        Permission permission = Permission.Create("admin.users", "Admin users");
        Role role = Role.Create("Ops", Now);
        db.Permissions.Add(permission);
        db.Roles.Add(role);
        db.RolePermissions.Add(RolePermission.Create(role.Id, permission.Id));
        db.UserRoles.Add(UserRole.Create(user.Id, role.Id, Now));
        await db.SaveChangesAsync();

        Assert.True(await evaluator.HasPermissionAsync(principal, "admin.users"));
    }

    private static async Task SeedMappedUserAsync(BreakGlassWebApplicationFactory factory, UserStatus status)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        User user = User.Create(MappedUpn, "Break Glass Mapped", UserType.Employee, Now, directoryObjectId: "bg-mapped");
        if (status == UserStatus.Disabled)
        {
            user.Disable(Now);
        }

        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    private static IdentityDbContext CreateIdentityDb()
    {
        DbContextOptions<IdentityDbContext> options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"break-glass-rbac-{Guid.NewGuid():N}")
            .Options;
        return new IdentityDbContext(options);
    }
}

internal sealed class BreakGlassWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly bool _enabled;
    private readonly string _databaseName = Guid.NewGuid().ToString("N");
    private readonly string _passwordHash;

    public BreakGlassWebApplicationFactory(bool enabled)
    {
        _enabled = enabled;
        _passwordHash = new PasswordHasher<object>().HashPassword(
            new object(),
            BreakGlassAuthenticationTestsPassword.Value);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Authentication:Oidc:Enabled", "false");
        builder.UseSetting("Authentication:BreakGlass:Enabled", _enabled ? "true" : "false");
        builder.UseSetting(
            "ConnectionStrings:QecItmg",
            "Server=(localdb)\\mssqllocaldb;Database=unused;Trusted_Connection=True;TrustServerCertificate=True");

        builder.ConfigureTestServices(services =>
        {
            RemoveDbContext<IdentityDbContext>(services);
            RemoveDbContext<OrganizationDbContext>(services);
            RemoveDbContext<PlatformDbContext>(services);
            RemoveDbContext<CmdbDbContext>(services);
            RemoveDbContext<ServiceDeskDbContext>(services);

            services.AddDbContext<IdentityDbContext>(options =>
                options.UseInMemoryDatabase($"bg-identity-{_databaseName}"));
            services.AddDbContext<OrganizationDbContext>(options =>
                options.UseInMemoryDatabase($"bg-organization-{_databaseName}"));
            services.AddDbContext<PlatformDbContext>(options =>
                options.UseInMemoryDatabase($"bg-platform-{_databaseName}"));
            services.AddDbContext<CmdbDbContext>(options =>
                options.UseInMemoryDatabase($"bg-cmdb-{_databaseName}"));
            services.AddDbContext<ServiceDeskDbContext>(options =>
                options.UseInMemoryDatabase($"bg-sd-{_databaseName}"));

            services.PostConfigure<BreakGlassAuthenticationOptions>(options =>
            {
                options.Enabled = _enabled;
                options.Accounts =
                [
                    new BreakGlassAccountOptions
                    {
                        Username = "breakglass.ops",
                        UserUpn = "breakglass@qehc.edu.sa",
                        PasswordHash = _passwordHash,
                    },
                ];
            });
        });
    }

    private static void RemoveDbContext<TContext>(IServiceCollection services)
        where TContext : DbContext
    {
        services.RemoveAll(typeof(DbContextOptions<TContext>));
        services.RemoveAll(typeof(IDbContextOptionsConfiguration<TContext>));
        services.RemoveAll(typeof(TContext));
    }
}

internal static class BreakGlassAuthenticationTestsPassword
{
    public const string Value = "Unit-Test-Only-Not-A-Secret!";
}
