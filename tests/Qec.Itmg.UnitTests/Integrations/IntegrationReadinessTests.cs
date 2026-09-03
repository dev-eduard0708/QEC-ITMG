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
using Microsoft.Extensions.Options;
using Qec.Itmg.Contracts.Integrations;
using Qec.Itmg.Identity.Domain;
using Qec.Itmg.Identity.Persistence;
using Qec.Itmg.Organization.Persistence;
using Qec.Itmg.Platform.Integrations;
using Qec.Itmg.Platform.Persistence;
using Xunit;

namespace Qec.Itmg.UnitTests.Integrations;

[Collection(WebApplicationFactoryCollection.Name)]
public sealed class IntegrationReadinessTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 17, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AllThreeAdapters_AreDisabledByDefault()
    {
        IntegrationOptions opts = new();

        IVeeamClient veeam = new DisabledVeeamClient(Options.Create(opts));
        ISonicWallCaptureClient sonicWall = new DisabledSonicWallClient(Options.Create(opts));
        ISynologyMonitor synology = new DisabledSynologyMonitor(Options.Create(opts));

        IntegrationReadiness veeamReady = veeam.GetReadiness();
        IntegrationReadiness sonicWallReady = sonicWall.GetReadiness();
        IntegrationReadiness synologyReady = synology.GetReadiness();

        Assert.False(veeamReady.Enabled);
        Assert.False(veeamReady.Configured);
        Assert.Equal(IntegrationRuntimeMode.Disabled, veeamReady.RuntimeMode);
        Assert.True(veeamReady.ApprovalRequired);

        Assert.False(sonicWallReady.Enabled);
        Assert.Equal(IntegrationRuntimeMode.Disabled, sonicWallReady.RuntimeMode);
        Assert.True(sonicWallReady.ApprovalRequired);

        Assert.False(synologyReady.Enabled);
        Assert.Equal(IntegrationRuntimeMode.Disabled, synologyReady.RuntimeMode);
        Assert.True(synologyReady.ApprovalRequired);
    }

    [Fact]
    public async Task DisabledAdapters_ThrowOnDataRequests_NotSilentSuccess()
    {
        IntegrationOptions opts = new();

        IVeeamClient veeam = new DisabledVeeamClient(Options.Create(opts));
        ISonicWallCaptureClient sonicWall = new DisabledSonicWallClient(Options.Create(opts));
        ISynologyMonitor synology = new DisabledSynologyMonitor(Options.Create(opts));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => veeam.GetRecentJobRunsAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sonicWall.GetEndpointsAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => synology.GetSystemSnapshotAsync());
    }

    [Fact]
    public async Task ReadinessEndpoint_RequiresAdminIntegrationsPermission_Anonymous401()
    {
        await using IntegrationReadinessWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        HttpResponseMessage response = await client.GetAsync("/api/v1/admin/integrations/readiness");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReadinessEndpoint_WithoutAdminIntegrationsPermission_Returns403()
    {
        await using IntegrationReadinessWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
        });

        await SeedAndSignInAsync(factory, client, permissionKey: "admin.users");
        HttpResponseMessage response = await client.GetAsync("/api/v1/admin/integrations/readiness");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ReadinessEndpoint_WithAdminIntegrationsPermission_ReturnsAllThreeDisabled()
    {
        await using IntegrationReadinessWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
        });

        await SeedAndSignInAsync(factory, client, permissionKey: "admin.integrations");
        HttpResponseMessage response = await client.GetAsync("/api/v1/admin/integrations/readiness");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement[] body = (await response.Content.ReadFromJsonAsync<JsonElement[]>())!;
        Assert.Equal(3, body.Length);

        foreach (JsonElement item in body)
        {
            Assert.False(item.GetProperty("enabled").GetBoolean());
            Assert.Equal("Disabled", item.GetProperty("runtimeMode").GetString());
            Assert.True(item.GetProperty("approvalRequired").GetBoolean());
        }

        JsonElement[] providers = body.Select(item => item.GetProperty("provider")).ToArray();
        Assert.Contains(providers, p => p.GetString() == "Veeam");
        Assert.Contains(providers, p => p.GetString() == "SonicWallCaptureClient");
        Assert.Contains(providers, p => p.GetString() == "Synology");
    }

    [Fact]
    public void ApplicationResolvesAllThreeIntegrationInterfaces()
    {
        using IntegrationReadinessWebApplicationFactory factory = new();
        using IServiceScope scope = factory.Services.CreateScope();

        IVeeamClient veeam = scope.ServiceProvider.GetRequiredService<IVeeamClient>();
        ISonicWallCaptureClient sonicWall = scope.ServiceProvider.GetRequiredService<ISonicWallCaptureClient>();
        ISynologyMonitor synology = scope.ServiceProvider.GetRequiredService<ISynologyMonitor>();

        Assert.IsType<DisabledVeeamClient>(veeam);
        Assert.IsType<DisabledSonicWallClient>(sonicWall);
        Assert.IsType<DisabledSynologyMonitor>(synology);
    }

    private static async Task SeedAndSignInAsync(
        IntegrationReadinessWebApplicationFactory factory,
        HttpClient client,
        string permissionKey)
    {
        string externalId = $"oid-{Guid.NewGuid():N}";
        string upn = $"{Guid.NewGuid():N}@qehc.edu.sa";

        using IServiceScope scope = factory.Services.CreateScope();
        IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        User user = User.Create(upn, "Test User", UserType.Employee, Now, directoryObjectId: externalId);
        Role role = Role.Create($"role-{Guid.NewGuid():N}"[..20], Now);
        Permission permission = Permission.Create(permissionKey);
        db.Users.Add(user);
        db.Roles.Add(role);
        db.Permissions.Add(permission);
        db.UserRoles.Add(UserRole.Create(user.Id, role.Id, Now));
        db.RolePermissions.Add(RolePermission.Create(role.Id, permission.Id));
        await db.SaveChangesAsync();

        HttpResponseMessage signIn = await client.GetAsync(
            $"/__test__/signin?externalId={Uri.EscapeDataString(externalId)}&upn={Uri.EscapeDataString(upn)}");
        Assert.Equal(HttpStatusCode.NoContent, signIn.StatusCode);
    }
}

internal sealed class IntegrationReadinessWebApplicationFactory : WebApplicationFactory<Program>
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
                options.UseInMemoryDatabase($"integ-identity-{_databaseName}"));
            services.AddDbContext<OrganizationDbContext>(options =>
                options.UseInMemoryDatabase($"integ-organization-{_databaseName}"));
            services.AddDbContext<PlatformDbContext>(options =>
                options.UseInMemoryDatabase($"integ-platform-{_databaseName}"));
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
