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
using Qec.Itmg.ServiceDesk.Domain;
using Qec.Itmg.ServiceDesk.Persistence;
using Qec.Itmg.ServiceDesk.Services;
using Xunit;

namespace Qec.Itmg.UnitTests.ServiceDesk;

[Collection(WebApplicationFactoryCollection.Name)]
public sealed class TicketApiTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 1, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task Employee_CreatedTicket_UsesCurrentUserAsRequester()
    {
        await using TicketApiWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
        });

        Guid userId = await SeedAndSignInAsync(factory, client, permissionKeys: []);

        HttpResponseMessage created = await client.PostAsJsonAsync(
            "/api/v1/me/tickets",
            new { type = "ServiceRequest", title = "VPN help", description = "Cannot connect" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        TicketDto? ticket = await created.Content.ReadFromJsonAsync<TicketDto>(JsonOptions);
        Assert.NotNull(ticket);
        Assert.Equal(userId, ticket!.RequesterUserId);
        Assert.StartsWith("SR-", ticket.TicketNumber);
    }

    [Fact]
    public async Task Employee_CannotReadAnotherEmployeesTicket()
    {
        await using TicketApiWebApplicationFactory factory = new();
        using HttpClient ownerClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
        });
        using HttpClient otherClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
        });

        Guid ownerId = await SeedAndSignInAsync(factory, ownerClient, permissionKeys: []);
        _ = await SeedAndSignInAsync(factory, otherClient, permissionKeys: []);

        Guid ticketId;
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            TicketService tickets = scope.ServiceProvider.GetRequiredService<TicketService>();
            Ticket ticket = await tickets.CreateAsync(
                TicketType.ServiceRequest,
                "Owner ticket",
                "Private",
                ownerId);
            ticketId = ticket.Id;
        }

        Assert.Equal(HttpStatusCode.OK, (await ownerClient.GetAsync($"/api/v1/me/tickets/{ticketId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherClient.GetAsync($"/api/v1/me/tickets/{ticketId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await otherClient.GetAsync("/api/v1/tickets")).StatusCode);
    }

    private static async Task<Guid> SeedAndSignInAsync(
        TicketApiWebApplicationFactory factory,
        HttpClient client,
        string[] permissionKeys)
    {
        string externalId = $"oid-{Guid.NewGuid():N}";
        string upn = $"{Guid.NewGuid():N}@qehc.edu.sa";
        Guid userId;

        using (IServiceScope scope = factory.Services.CreateScope())
        {
            IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            User user = User.Create(upn, "Ticket Tester", UserType.Employee, Now, directoryObjectId: externalId);
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

internal sealed class TicketApiWebApplicationFactory : WebApplicationFactory<Program>
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
                options.UseInMemoryDatabase($"sd-api-identity-{_databaseName}"));
            services.AddDbContext<OrganizationDbContext>(options =>
                options.UseInMemoryDatabase($"sd-api-organization-{_databaseName}"));
            services.AddDbContext<PlatformDbContext>(options =>
                options.UseInMemoryDatabase($"sd-api-platform-{_databaseName}"));
            services.AddDbContext<CmdbDbContext>(options =>
                options.UseInMemoryDatabase($"sd-api-cmdb-{_databaseName}"));
            services.AddDbContext<ServiceDeskDbContext>(options =>
                options.UseInMemoryDatabase($"sd-api-sd-{_databaseName}"));
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
