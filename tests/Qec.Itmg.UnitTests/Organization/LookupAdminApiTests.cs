using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Qec.Itmg.Identity.Domain;
using Qec.Itmg.Identity.Persistence;
using Qec.Itmg.Organization.Admin;
using Qec.Itmg.Cmdb.Persistence;
using Qec.Itmg.Organization.Persistence;
using Qec.Itmg.Platform.Persistence;
using Xunit;

namespace Qec.Itmg.UnitTests.Organization;

[Collection(WebApplicationFactoryCollection.Name)]
public sealed class LookupAdminApiTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Employee_WithoutLookupsPermission_Gets403()
    {
        await using LookupAdminWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
        });

        await SeedAndSignInAsync(factory, client, permissionKey: "admin.users");

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/lookups/departments")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/lookups/locations")).StatusCode);
    }

    [Fact]
    public async Task AdminWithLookupsPermission_CanCreateAndUpdateDepartment()
    {
        await using LookupAdminWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
        });

        await SeedAndSignInAsync(factory, client, permissionKey: "admin.lookups");

        HttpResponseMessage created = await client.PostAsJsonAsync(
            "/api/v1/admin/lookups/departments",
            new CreateLookupItemRequest("Finance", "Finance dept"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        LookupItemDto? department = await created.Content.ReadFromJsonAsync<LookupItemDto>();
        Assert.NotNull(department);
        Assert.Equal("Finance", department!.Name);
        Assert.True(department.IsActive);

        HttpResponseMessage updated = await client.PutAsJsonAsync(
            $"/api/v1/admin/lookups/departments/{department.Id}",
            new UpdateLookupItemRequest("Finance Ops", "Updated", false, department.RowVersion));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        LookupItemDto? after = await updated.Content.ReadFromJsonAsync<LookupItemDto>();
        Assert.NotNull(after);
        Assert.Equal("Finance Ops", after!.Name);
        Assert.False(after.IsActive);

        HttpResponseMessage list = await client.GetAsync("/api/v1/admin/lookups/departments");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        List<LookupItemDto>? items = await list.Content.ReadFromJsonAsync<List<LookupItemDto>>();
        Assert.Contains(items!, item => item.Name == "Finance Ops" && !item.IsActive);
    }

    [Fact]
    public async Task AdminWithLookupsPermission_CanCreateLocation()
    {
        await using LookupAdminWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
        });

        await SeedAndSignInAsync(factory, client, permissionKey: "admin.lookups");

        HttpResponseMessage created = await client.PostAsJsonAsync(
            "/api/v1/admin/lookups/locations",
            new CreateLookupItemRequest("Riyadh HQ", null));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        LookupItemDto? location = await created.Content.ReadFromJsonAsync<LookupItemDto>();
        Assert.NotNull(location);
        Assert.Equal("Riyadh HQ", location!.Name);
    }

    private static async Task SeedAndSignInAsync(
        LookupAdminWebApplicationFactory factory,
        HttpClient client,
        string permissionKey)
    {
        string externalId = $"oid-{Guid.NewGuid():N}";
        string upn = $"{Guid.NewGuid():N}@qehc.edu.sa";

        using (IServiceScope scope = factory.Services.CreateScope())
        {
            IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            User user = User.Create(upn, "Lookup Tester", UserType.Employee, Now, directoryObjectId: externalId);
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

internal sealed class LookupAdminWebApplicationFactory : WebApplicationFactory<Program>
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
            RemoveDbContext<CmdbDbContext>(services);

            services.AddDbContext<IdentityDbContext>(options =>
                options.UseInMemoryDatabase($"lookup-identity-{_databaseName}"));
            services.AddDbContext<OrganizationDbContext>(options =>
                options.UseInMemoryDatabase($"lookup-organization-{_databaseName}"));
            services.AddDbContext<PlatformDbContext>(options =>
                options.UseInMemoryDatabase($"lookup-platform-{_databaseName}"));
            services.AddDbContext<CmdbDbContext>(options =>
                options.UseInMemoryDatabase($"lookup-cmdb-{_databaseName}"));
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
