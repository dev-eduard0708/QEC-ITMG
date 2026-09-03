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
using Qec.Itmg.Cmdb.Persistence;
using Qec.Itmg.Identity.Domain;
using Qec.Itmg.Identity.Persistence;
using Qec.Itmg.Organization.Persistence;
using Qec.Itmg.Platform.Persistence;
using Qec.Itmg.ServiceDesk.Persistence;
using Qec.Itmg.ServiceDesk.Services;
using Xunit;

namespace Qec.Itmg.UnitTests.ServiceDesk;

[Collection(WebApplicationFactoryCollection.Name)]
public sealed class KnowledgeBaseAndDashboardApiTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 4, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task Published_VisibleToEmployee_Draft_Hidden()
    {
        await using KbWebApplicationFactory factory = new();
        using HttpClient employee = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        using HttpClient admin = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

        _ = await SeedAndSignInAsync(factory, employee, []);
        _ = await SeedAndSignInAsync(factory, admin, ["kb.read", "kb.manage"]);

        HttpResponseMessage draftCreate = await admin.PostAsJsonAsync(
            "/api/v1/kb/admin",
            new { title = "Draft Only", slug = "draft-only", body = "Secret draft", summary = "draft" });
        Assert.Equal(HttpStatusCode.Created, draftCreate.StatusCode);

        HttpResponseMessage publishedCreate = await admin.PostAsJsonAsync(
            "/api/v1/kb/admin",
            new { title = "VPN Help", slug = "vpn-help", body = "Reset VPN", summary = "vpn" });
        Assert.Equal(HttpStatusCode.Created, publishedCreate.StatusCode);
        KnowledgeArticleDto? published = await publishedCreate.Content.ReadFromJsonAsync<KnowledgeArticleDto>(JsonOptions);
        Assert.NotNull(published);

        Assert.Equal(
            HttpStatusCode.OK,
            (await admin.PostAsync($"/api/v1/kb/admin/{published!.Id}/publish", null)).StatusCode);

        List<KnowledgeArticleDto>? employeeList =
            await employee.GetFromJsonAsync<List<KnowledgeArticleDto>>("/api/v1/kb", JsonOptions);
        Assert.NotNull(employeeList);
        Assert.Contains(employeeList!, item => item.Slug == "vpn-help");
        Assert.DoesNotContain(employeeList!, item => item.Slug == "draft-only");

        Assert.Equal(HttpStatusCode.OK, (await employee.GetAsync("/api/v1/kb/vpn-help")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await employee.GetAsync("/api/v1/kb/draft-only")).StatusCode);
    }

    [Fact]
    public async Task KbManage_RequiresPermission()
    {
        await using KbWebApplicationFactory factory = new();
        using HttpClient employee = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        _ = await SeedAndSignInAsync(factory, employee, []);

        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync("/api/v1/kb/admin")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await employee.PostAsJsonAsync(
                "/api/v1/kb/admin",
                new { title = "X", slug = "x", body = "Y" })).StatusCode);
    }

    [Fact]
    public async Task Dashboard_ReturnsBasicCounts()
    {
        await using KbWebApplicationFactory factory = new();
        using HttpClient it = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        Guid itUserId = await SeedAndSignInAsync(factory, it, ["tickets.read", "tickets.manage"]);

        using (IServiceScope scope = factory.Services.CreateScope())
        {
            TicketService tickets = scope.ServiceProvider.GetRequiredService<TicketService>();
            await tickets.CreateAsync(
                Qec.Itmg.ServiceDesk.Domain.TicketType.Incident,
                "Critical outage",
                "Down",
                Guid.CreateVersion7(),
                Qec.Itmg.ServiceDesk.Domain.TicketPriority.Critical);
            var assigned = await tickets.CreateAsync(
                Qec.Itmg.ServiceDesk.Domain.TicketType.ServiceRequest,
                "Assigned one",
                "Body",
                Guid.CreateVersion7());
            await tickets.AssignAsync(assigned.Id, itUserId, null, itUserId);
        }

        TicketDashboardDto? dashboard = await it.GetFromJsonAsync<TicketDashboardDto>(
            "/api/v1/tickets/dashboard",
            JsonOptions);
        Assert.NotNull(dashboard);
        Assert.True(dashboard!.OpenTickets >= 2);
        Assert.True(dashboard.CriticalOpen >= 1);
        Assert.True(dashboard.MyAssigned >= 1);
        Assert.True(dashboard.NewToday >= 2);
        Assert.NotEmpty(dashboard.ByStatus);
    }

    private static async Task<Guid> SeedAndSignInAsync(
        KbWebApplicationFactory factory,
        HttpClient client,
        string[] permissionKeys)
    {
        string externalId = $"oid-{Guid.NewGuid():N}";
        string upn = $"{Guid.NewGuid():N}@qehc.edu.sa";
        Guid userId;

        using (IServiceScope scope = factory.Services.CreateScope())
        {
            IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            User user = User.Create(upn, "KB Tester", UserType.Employee, Now, directoryObjectId: externalId);
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
        return userId;
    }
}

internal sealed class KbWebApplicationFactory : WebApplicationFactory<Program>
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
                options.UseInMemoryDatabase($"kb-identity-{_databaseName}"));
            services.AddDbContext<OrganizationDbContext>(options =>
                options.UseInMemoryDatabase($"kb-organization-{_databaseName}"));
            services.AddDbContext<PlatformDbContext>(options =>
                options.UseInMemoryDatabase($"kb-platform-{_databaseName}"));
            services.AddDbContext<CmdbDbContext>(options =>
                options.UseInMemoryDatabase($"kb-cmdb-{_databaseName}"));
            services.AddDbContext<ServiceDeskDbContext>(options =>
                options.UseInMemoryDatabase($"kb-sd-{_databaseName}"));
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
