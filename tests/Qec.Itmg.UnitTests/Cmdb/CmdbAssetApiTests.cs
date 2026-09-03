using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Qec.Itmg.Cmdb.Domain;
using Qec.Itmg.Cmdb.Persistence;
using Qec.Itmg.ServiceDesk.Persistence;
using Qec.Itmg.Cmdb.Services;
using Qec.Itmg.Identity.Domain;
using Qec.Itmg.Identity.Persistence;
using Qec.Itmg.Organization.Persistence;
using Qec.Itmg.Platform.Persistence;
using Xunit;

namespace Qec.Itmg.UnitTests.Cmdb;

[Collection(WebApplicationFactoryCollection.Name)]
public sealed class CmdbAssetApiTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 0, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task ManagePermission_CanCreateAssetAndAssignReturn()
    {
        await using CmdbApiWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
        });

        Guid adminUserId = await SeedAndSignInAsync(
            factory,
            client,
            permissionKeys: ["assets.manage", "assets.read", "cmdb.manage", "cmdb.read"]);

        HttpResponseMessage created = await client.PostAsJsonAsync(
            "/api/v1/assets",
            new
            {
                assetType = "Laptop",
                name = "Test Laptop",
                serialNumber = "SN-100",
            });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        AssetDto? asset = await created.Content.ReadFromJsonAsync<AssetDto>(JsonOptions);
        Assert.NotNull(asset);
        Assert.StartsWith("AST-", asset!.AssetNumber);

        Guid employeeId;
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            IdentityDbContext idb = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            User employee = User.Create(
                $"{Guid.NewGuid():N}@qehc.edu.sa",
                "Assignee",
                UserType.Employee,
                Now);
            idb.Users.Add(employee);
            await idb.SaveChangesAsync();
            employeeId = employee.Id;
        }

        HttpResponseMessage assigned = await client.PostAsJsonAsync(
            $"/api/v1/assets/{asset.Id}/assign",
            new { assignedToUserId = employeeId, notes = "desk issue" });
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);

        AssetAssignmentDto? assignment =
            await assigned.Content.ReadFromJsonAsync<AssetAssignmentDto>(JsonOptions);
        Assert.NotNull(assignment);
        Assert.Equal(employeeId, assignment!.AssignedToUserId);
        Assert.Equal(adminUserId, assignment.AssignedByUserId);
        Assert.True(assignment.IsActive);
        Assert.Null(assignment.ReturnedAtUtc);

        HttpResponseMessage returned = await client.PostAsJsonAsync(
            $"/api/v1/assets/{asset.Id}/return",
            new { notes = "returned to stock" });
        Assert.Equal(HttpStatusCode.OK, returned.StatusCode);

        AssetAssignmentDto? closed =
            await returned.Content.ReadFromJsonAsync<AssetAssignmentDto>(JsonOptions);
        Assert.NotNull(closed);
        Assert.NotNull(closed!.ReturnedAtUtc);
        Assert.False(closed.IsActive);
    }

    [Fact]
    public async Task Unauthorized_WithoutAssetsPermission_Gets403()
    {
        await using CmdbApiWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
        });

        await SeedAndSignInAsync(factory, client, permissionKeys: ["admin.users"]);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/assets")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/cmdb/cis")).StatusCode);
    }

    [Fact]
    public async Task MeEquipment_ReturnsOnlyOwnActiveAssignments()
    {
        await using CmdbApiWebApplicationFactory factory = new();
        using HttpClient ownerClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
        });
        using HttpClient otherClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
        });

        (Guid ownerId, _) = await SeedUserAsync(factory, ownerClient, permissionKeys: []);
        (Guid otherId, _) = await SeedUserAsync(factory, otherClient, permissionKeys: []);

        Guid assetForOwner;
        Guid assetForOther;
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            AssetService assets = scope.ServiceProvider.GetRequiredService<AssetService>();
            Asset a1 = await assets.CreateAsync("Laptop", "Owner Laptop");
            Asset a2 = await assets.CreateAsync("Laptop", "Other Laptop");
            await assets.AssignAsync(a1.Id, ownerId, ownerId);
            await assets.AssignAsync(a2.Id, otherId, otherId);
            assetForOwner = a1.Id;
            assetForOther = a2.Id;
        }

        HttpResponseMessage response = await ownerClient.GetAsync("/api/v1/me/equipment");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        List<AssetDto>? items = await response.Content.ReadFromJsonAsync<List<AssetDto>>(JsonOptions);
        Assert.NotNull(items);
        Assert.Contains(items!, item => item.Id == assetForOwner);
        Assert.DoesNotContain(items!, item => item.Id == assetForOther);

        Assert.Equal(HttpStatusCode.Forbidden, (await ownerClient.GetAsync("/api/v1/assets")).StatusCode);
    }

    [Fact]
    public async Task CmdbManage_CanCreateConfigurationItem()
    {
        await using CmdbApiWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
        });

        await SeedAndSignInAsync(factory, client, permissionKeys: ["cmdb.manage", "cmdb.read"]);

        Guid typeId;
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            ConfigurationItemService cis = scope.ServiceProvider.GetRequiredService<ConfigurationItemService>();
            CiType type = await cis.CreateCiTypeAsync("laptop", "Laptop");
            typeId = type.Id;
        }

        HttpResponseMessage created = await client.PostAsJsonAsync(
            "/api/v1/cmdb/cis",
            new { ciTypeId = typeId, name = "Finance App", criticality = "High" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        HttpResponseMessage list = await client.GetAsync("/api/v1/cmdb/cis");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
    }

    private static async Task<Guid> SeedAndSignInAsync(
        CmdbApiWebApplicationFactory factory,
        HttpClient client,
        string[] permissionKeys)
    {
        (Guid userId, _) = await SeedUserAsync(factory, client, permissionKeys);
        return userId;
    }

    private static async Task<(Guid UserId, string ExternalId)> SeedUserAsync(
        CmdbApiWebApplicationFactory factory,
        HttpClient client,
        string[] permissionKeys)
    {
        string externalId = $"oid-{Guid.NewGuid():N}";
        string upn = $"{Guid.NewGuid():N}@qehc.edu.sa";
        Guid userId;

        using (IServiceScope scope = factory.Services.CreateScope())
        {
            IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            User user = User.Create(upn, "Cmdb Tester", UserType.Employee, Now, directoryObjectId: externalId);
            db.Users.Add(user);

            if (permissionKeys.Length > 0)
            {
                Role role = Role.Create($"role-{Guid.NewGuid():N}"[..20], Now);
                db.Roles.Add(role);
                db.UserRoles.Add(UserRole.Create(user.Id, role.Id, Now));
                foreach (string key in permissionKeys)
                {
                    Permission permission = Permission.Create(key);
                    db.Permissions.Add(permission);
                    db.RolePermissions.Add(RolePermission.Create(role.Id, permission.Id));
                }
            }

            await db.SaveChangesAsync();
            userId = user.Id;
        }

        HttpResponseMessage signIn = await client.GetAsync(
            $"/__test__/signin?externalId={Uri.EscapeDataString(externalId)}&upn={Uri.EscapeDataString(upn)}");
        Assert.Equal(HttpStatusCode.NoContent, signIn.StatusCode);
        return (userId, externalId);
    }
}

internal sealed class CmdbApiWebApplicationFactory : WebApplicationFactory<Program>
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
            RemoveDbContext<ServiceDeskDbContext>(services);

            services.AddDbContext<IdentityDbContext>(options =>
                options.UseInMemoryDatabase($"cmdb-api-identity-{_databaseName}"));
            services.AddDbContext<OrganizationDbContext>(options =>
                options.UseInMemoryDatabase($"cmdb-api-organization-{_databaseName}"));
            services.AddDbContext<PlatformDbContext>(options =>
                options.UseInMemoryDatabase($"cmdb-api-platform-{_databaseName}"));
            services.AddDbContext<CmdbDbContext>(options =>
                options.UseInMemoryDatabase($"cmdb-api-cmdb-{_databaseName}"));
            services.AddDbContext<ServiceDeskDbContext>(options =>
                options.UseInMemoryDatabase($"cmdb-api-sd-{_databaseName}"));
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
