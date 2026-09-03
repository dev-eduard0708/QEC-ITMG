using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Qec.Itmg.Identity.Admin;
using Qec.Itmg.Identity.Authentication;
using Qec.Itmg.Identity.Domain;
using Qec.Itmg.Identity.Persistence;
using Qec.Itmg.Organization.Persistence;
using Qec.Itmg.Platform.Persistence;
using Xunit;

namespace Qec.Itmg.UnitTests.Admin;

public sealed class AdminApiAuthorizationTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task UsersEndpoints_RequireAdminUsersPermission()
    {
        await using AdminApiWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        HttpResponseMessage anonymous = await client.GetAsync("/api/v1/admin/users");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        await SeedAndSignInAsync(factory, client, permissionKey: "admin.roles");
        HttpResponseMessage forbidden = await client.GetAsync("/api/v1/admin/users");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        await using AdminApiWebApplicationFactory allowedFactory = new();
        using HttpClient allowedClient = allowedFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
        });
        await SeedAndSignInAsync(allowedFactory, allowedClient, permissionKey: "admin.users");
        HttpResponseMessage ok = await allowedClient.GetAsync("/api/v1/admin/users");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Fact]
    public async Task RolesAndPermissionsEndpoints_RequireAdminRolesPermission()
    {
        await using AdminApiWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
        });

        await SeedAndSignInAsync(factory, client, permissionKey: "admin.users");
        HttpResponseMessage forbidden = await client.GetAsync("/api/v1/admin/roles");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        HttpResponseMessage permissionsForbidden = await client.GetAsync("/api/v1/admin/permissions");
        Assert.Equal(HttpStatusCode.Forbidden, permissionsForbidden.StatusCode);

        await using AdminApiWebApplicationFactory allowedFactory = new();
        using HttpClient allowedClient = allowedFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
        });
        await SeedAndSignInAsync(allowedFactory, allowedClient, permissionKey: "admin.roles");
        Assert.Equal(HttpStatusCode.OK, (await allowedClient.GetAsync("/api/v1/admin/roles")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await allowedClient.GetAsync("/api/v1/admin/permissions")).StatusCode);
    }

    [Fact]
    public async Task DisabledUser_IsDeniedEvenWithPermissionAssignment()
    {
        await using AdminApiWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
        });

        await SeedAndSignInAsync(factory, client, permissionKey: "admin.users", status: UserStatus.Disabled);
        HttpResponseMessage response = await client.GetAsync("/api/v1/admin/users");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UsersApi_CreateListAndAssignRoles_WorksWhenAuthorized()
    {
        await using AdminApiWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
        });

        await SeedAndSignInAsync(factory, client, permissionKey: "admin.users");

        using (IServiceScope scope = factory.Services.CreateScope())
        {
            IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            db.Roles.Add(Role.Create("Assignable", Now));
            await db.SaveChangesAsync();
        }

        HttpResponseMessage created = await client.PostAsJsonAsync(
            "/api/v1/admin/users",
            new CreateAdminUserRequest("created@qehc.edu.sa", "Created User", "Employee", null, null));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        AdminUserDto? createdUser = await created.Content.ReadFromJsonAsync<AdminUserDto>();
        Assert.NotNull(createdUser);

        Guid roleId;
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            roleId = await db.Roles.Where(role => role.Name == "Assignable").Select(role => role.Id).SingleAsync();
        }

        HttpResponseMessage rolesAssigned = await client.PutAsJsonAsync(
            $"/api/v1/admin/users/{createdUser!.Id}/roles",
            new ReplaceUserRolesRequest([roleId]));
        Assert.Equal(HttpStatusCode.OK, rolesAssigned.StatusCode);

        HttpResponseMessage list = await client.GetAsync("/api/v1/admin/users?search=created@");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        List<AdminUserDto>? users = await list.Content.ReadFromJsonAsync<List<AdminUserDto>>();
        Assert.NotNull(users);
        Assert.Contains(users!, user => user.Upn == "created@qehc.edu.sa" && user.Roles.Any(role => role.Name == "Assignable"));
    }

    private static async Task SeedAndSignInAsync(
        AdminApiWebApplicationFactory factory,
        HttpClient client,
        string permissionKey,
        UserStatus status = UserStatus.Active)
    {
        string externalId = $"oid-{Guid.NewGuid():N}";
        string upn = $"{Guid.NewGuid():N}@qehc.edu.sa";

        using (IServiceScope scope = factory.Services.CreateScope())
        {
            IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            User user = User.Create(upn, "API Tester", UserType.Employee, Now, directoryObjectId: externalId);
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
        }

        HttpResponseMessage signIn = await client.GetAsync(
            $"/__test__/signin?externalId={Uri.EscapeDataString(externalId)}&upn={Uri.EscapeDataString(upn)}");
        Assert.Equal(HttpStatusCode.NoContent, signIn.StatusCode);
    }
}

internal sealed class AdminApiWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Authentication:Oidc:Enabled", "false");
        builder.UseSetting(
            "ConnectionStrings:QecItmg",
            "Server=(localdb)\\mssqllocaldb;Database=unused;Trusted_Connection=True;TrustServerCertificate=True");

        builder.ConfigureTestServices(services =>
        {
            RemoveDbContext<IdentityDbContext>(services);
            RemoveDbContext<OrganizationDbContext>(services);
            RemoveDbContext<PlatformDbContext>(services);

            services.AddDbContext<IdentityDbContext>(options =>
                options.UseInMemoryDatabase($"identity-{_databaseName}"));
            services.AddDbContext<OrganizationDbContext>(options =>
                options.UseInMemoryDatabase($"organization-{_databaseName}"));
            services.AddDbContext<PlatformDbContext>(options =>
                options.UseInMemoryDatabase($"platform-{_databaseName}"));
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
