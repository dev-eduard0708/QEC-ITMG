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
using Qec.Itmg.Identity.Authentication;
using Qec.Itmg.Identity.CurrentUser;
using Qec.Itmg.Identity.Domain;
using Qec.Itmg.Identity.Persistence;
using Qec.Itmg.Organization.Persistence;
using Qec.Itmg.Platform.Persistence;
using Xunit;

namespace Qec.Itmg.UnitTests.CurrentUser;

[Collection(WebApplicationFactoryCollection.Name)]
public sealed class CurrentUserMeEndpointTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Me_Unauthenticated_Returns401()
    {
        await using MeWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_JitProvisionsGoogleUser_WithEmployeeRole()
    {
        await using MeWebApplicationFactory factory = new();
        await SeedEmployeeRoleAsync(factory);

        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
        });

        string externalId = $"google-{Guid.NewGuid():N}";
        string upn = $"jit-{Guid.NewGuid():N}@qehc.edu.sa";
        HttpResponseMessage signIn = await client.GetAsync(
            $"/__test__/signin?externalId={Uri.EscapeDataString(externalId)}&upn={Uri.EscapeDataString(upn)}&name=JIT%20User");
        Assert.Equal(HttpStatusCode.NoContent, signIn.StatusCode);

        HttpResponseMessage me = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        using JsonDocument body = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        Assert.Equal(upn, body.RootElement.GetProperty("upn").GetString());
        Assert.Equal("Google", body.RootElement.GetProperty("authMethod").GetString());
        Assert.Equal("JIT User", body.RootElement.GetProperty("displayName").GetString());
        Assert.Contains(
            body.RootElement.GetProperty("roles").EnumerateArray(),
            role => role.GetProperty("name").GetString() == CurrentUserService.EmployeeRoleName);

        using IServiceScope scope = factory.Services.CreateScope();
        IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        User? stored = await db.Users.SingleOrDefaultAsync(user => user.Upn == upn);
        Assert.NotNull(stored);
        Assert.Equal(externalId, stored!.DirectoryObjectId);
    }

    [Fact]
    public async Task Me_BindsGoogleSub_ToPreProvisionedUser()
    {
        await using MeWebApplicationFactory factory = new();
        string upn = $"pre-{Guid.NewGuid():N}@qehc.edu.sa";
        string externalId = $"google-bind-{Guid.NewGuid():N}";

        using (IServiceScope scope = factory.Services.CreateScope())
        {
            IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            db.Users.Add(User.Create(upn, "Pre Provisioned", UserType.Employee, Now));
            await db.SaveChangesAsync();
        }

        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
        });
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.GetAsync(
                $"/__test__/signin?externalId={Uri.EscapeDataString(externalId)}&upn={Uri.EscapeDataString(upn)}")).StatusCode);

        HttpResponseMessage me = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        using IServiceScope verify = factory.Services.CreateScope();
        IdentityDbContext verifyDb = verify.ServiceProvider.GetRequiredService<IdentityDbContext>();
        User stored = await verifyDb.Users.SingleAsync(user => user.Upn == upn);
        Assert.Equal(externalId, stored.DirectoryObjectId);
    }

    [Fact]
    public async Task Me_BreakGlass_DoesNotJit()
    {
        await using MeWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
        });

        string externalId = $"break-glass:{Guid.NewGuid():N}";
        string upn = $"missing-bg-{Guid.NewGuid():N}@qehc.edu.sa";

        // Sign in as break-glass style principal without a mapped user.
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            // Direct cookie via test endpoint cannot set break-glass claim; use custom sign-in path.
        }

        HttpResponseMessage signIn = await client.GetAsync(
            $"/__test__/signin-break-glass?externalId={Uri.EscapeDataString(externalId)}&upn={Uri.EscapeDataString(upn)}");
        Assert.Equal(HttpStatusCode.NoContent, signIn.StatusCode);

        HttpResponseMessage me = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.Forbidden, me.StatusCode);

        using IServiceScope verify = factory.Services.CreateScope();
        IdentityDbContext db = verify.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.False(await db.Users.AnyAsync(user => user.Upn == upn));
    }

    [Fact]
    public async Task Me_Employee_HasNoAdminPermissions()
    {
        await using MeWebApplicationFactory factory = new();
        await SeedEmployeeRoleAsync(factory);

        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
        });

        string externalId = $"google-{Guid.NewGuid():N}";
        string upn = $"emp-{Guid.NewGuid():N}@qehc.edu.sa";
        await client.GetAsync(
            $"/__test__/signin?externalId={Uri.EscapeDataString(externalId)}&upn={Uri.EscapeDataString(upn)}");

        HttpResponseMessage me = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        Assert.Empty(body.RootElement.GetProperty("permissions").EnumerateArray());

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/users")).StatusCode);
    }

    private static async Task SeedEmployeeRoleAsync(MeWebApplicationFactory factory)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        if (!await db.Roles.AnyAsync(role => role.Name == CurrentUserService.EmployeeRoleName))
        {
            db.Roles.Add(Role.Create(CurrentUserService.EmployeeRoleName, Now, isSystem: true));
            await db.SaveChangesAsync();
        }
    }
}

internal sealed class MeWebApplicationFactory : WebApplicationFactory<Program>
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
                options.UseInMemoryDatabase($"me-identity-{_databaseName}"));
            services.AddDbContext<OrganizationDbContext>(options =>
                options.UseInMemoryDatabase($"me-organization-{_databaseName}"));
            services.AddDbContext<PlatformDbContext>(options =>
                options.UseInMemoryDatabase($"me-platform-{_databaseName}"));
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
