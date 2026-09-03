using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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

/// <summary>
/// P4-09 minimal IDOR/authz closeout — only gaps not already covered elsewhere.
/// </summary>
[Collection(WebApplicationFactoryCollection.Name)]
public sealed class TicketAuthzCloseoutTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 3, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task Employee_CannotCommentOrUploadOnAnotherEmployeesTicket()
    {
        await using TicketAuthzWebApplicationFactory factory = new();
        using HttpClient owner = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        using HttpClient other = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

        Guid ownerId = await SeedAndSignInAsync(factory, owner, []);
        _ = await SeedAndSignInAsync(factory, other, []);
        Guid ticketId = await CreateTicketAsync(factory, ownerId, "IDOR target");

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await other.PostAsJsonAsync(
                $"/api/v1/me/tickets/{ticketId}/comments",
                new { body = "Hijack comment" })).StatusCode);

        using MultipartFormDataContent form = new();
        ByteArrayContent file = new(Encoding.UTF8.GetBytes("stolen"));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", "x.txt");
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await other.PostAsync($"/api/v1/me/tickets/{ticketId}/attachments", form)).StatusCode);
    }

    [Fact]
    public async Task Employee_CannotCallGlobalTicketManagementEndpoints()
    {
        await using TicketAuthzWebApplicationFactory factory = new();
        using HttpClient employee = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        Guid employeeId = await SeedAndSignInAsync(factory, employee, []);
        Guid ticketId = await CreateTicketAsync(factory, employeeId, "No global access");

        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync("/api/v1/tickets")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync($"/api/v1/tickets/{ticketId}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await employee.PostAsJsonAsync(
                $"/api/v1/tickets/{ticketId}/status",
                new { status = "InProgress" })).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await employee.PostAsJsonAsync(
                $"/api/v1/tickets/{ticketId}/assign",
                new { assignedUserId = employeeId })).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await employee.PostAsJsonAsync(
                $"/api/v1/tickets/{ticketId}/comments",
                new { body = "nope", visibility = "Internal" })).StatusCode);
    }

    [Fact]
    public async Task ItWithPermission_CanListAndGetTicket()
    {
        await using TicketAuthzWebApplicationFactory factory = new();
        using HttpClient it = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        using HttpClient employee = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

        _ = await SeedAndSignInAsync(factory, it, ["tickets.read", "tickets.manage"]);
        Guid employeeId = await SeedAndSignInAsync(factory, employee, []);
        Guid ticketId = await CreateTicketAsync(factory, employeeId, "IT can see");

        Assert.Equal(HttpStatusCode.OK, (await it.GetAsync("/api/v1/tickets")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await it.GetAsync($"/api/v1/tickets/{ticketId}")).StatusCode);
    }

    [Fact]
    public async Task EmployeeRequestFlow_CreateListDetailComment_ItAssignStatus_EmployeeSeesUpdate()
    {
        await using TicketAuthzWebApplicationFactory factory = new();
        using HttpClient employee = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        using HttpClient it = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

        _ = await SeedAndSignInAsync(factory, employee, []);
        Guid itUserId = await SeedAndSignInAsync(factory, it, ["tickets.read", "tickets.manage"]);

        HttpResponseMessage created = await employee.PostAsJsonAsync(
            "/api/v1/me/tickets",
            new
            {
                type = "ServiceRequest",
                title = "Flow SR",
                description = "Need access",
                priority = "Medium",
            });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        TicketDto? ticket = await created.Content.ReadFromJsonAsync<TicketDto>(JsonOptions);
        Assert.NotNull(ticket);
        Assert.StartsWith("SR-", ticket!.TicketNumber);

        TicketListResult? list = await employee.GetFromJsonAsync<TicketListResult>("/api/v1/me/tickets", JsonOptions);
        Assert.NotNull(list);
        Assert.Contains(list!.Items, item => item.Id == ticket.Id);

        Assert.Equal(HttpStatusCode.OK, (await employee.GetAsync($"/api/v1/me/tickets/{ticket.Id}")).StatusCode);

        Assert.Equal(
            HttpStatusCode.Created,
            (await employee.PostAsJsonAsync(
                $"/api/v1/me/tickets/{ticket.Id}/comments",
                new { body = "More details" })).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await it.GetAsync($"/api/v1/tickets/{ticket.Id}")).StatusCode);

        HttpResponseMessage assigned = await it.PostAsJsonAsync(
            $"/api/v1/tickets/{ticket.Id}/assign",
            new { assignedUserId = itUserId, notes = "taking" });
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        TicketDto? afterAssign = await assigned.Content.ReadFromJsonAsync<TicketDto>(JsonOptions);
        Assert.NotNull(afterAssign);

        HttpResponseMessage status = await it.PostAsJsonAsync(
            $"/api/v1/tickets/{ticket.Id}/status",
            new { status = "InProgress", rowVersion = afterAssign!.RowVersion });
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);

        TicketDto? employeeView = await employee.GetFromJsonAsync<TicketDto>(
            $"/api/v1/me/tickets/{ticket.Id}",
            JsonOptions);
        Assert.NotNull(employeeView);
        Assert.Equal("InProgress", employeeView!.Status);
    }

    private static async Task<Guid> CreateTicketAsync(
        TicketAuthzWebApplicationFactory factory,
        Guid requesterId,
        string title)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        TicketService tickets = scope.ServiceProvider.GetRequiredService<TicketService>();
        Ticket ticket = await tickets.CreateAsync(
            TicketType.ServiceRequest,
            title,
            "Details",
            requesterId);
        return ticket.Id;
    }

    private static async Task<Guid> SeedAndSignInAsync(
        TicketAuthzWebApplicationFactory factory,
        HttpClient client,
        string[] permissionKeys)
    {
        string externalId = $"oid-{Guid.NewGuid():N}";
        string upn = $"{Guid.NewGuid():N}@qehc.edu.sa";
        Guid userId;

        using (IServiceScope scope = factory.Services.CreateScope())
        {
            IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            User user = User.Create(upn, "Authz Tester", UserType.Employee, Now, directoryObjectId: externalId);
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

internal sealed class TicketAuthzWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Authentication:Oidc:Enabled", "false");
        builder.UseSetting(
            "ConnectionStrings:QecItmg",
            "Server=(localdb)\\mssqllocaldb;Database=unused;Trusted_Connection=True;TrustServerCertificate=True");
        builder.UseSetting("Platform:Attachments:RootPath", Path.Combine(Path.GetTempPath(), "qec-itmg-att", _databaseName));

        builder.ConfigureTestServices(services =>
        {
            RemoveDbContext<IdentityDbContext>(services);
            RemoveDbContext<OrganizationDbContext>(services);
            RemoveDbContext<PlatformDbContext>(services);
            RemoveDbContext<CmdbDbContext>(services);
            RemoveDbContext<ServiceDeskDbContext>(services);

            services.AddDbContext<IdentityDbContext>(options =>
                options.UseInMemoryDatabase($"sd-authz-identity-{_databaseName}"));
            services.AddDbContext<OrganizationDbContext>(options =>
                options.UseInMemoryDatabase($"sd-authz-organization-{_databaseName}"));
            services.AddDbContext<PlatformDbContext>(options =>
                options.UseInMemoryDatabase($"sd-authz-platform-{_databaseName}"));
            services.AddDbContext<CmdbDbContext>(options =>
                options.UseInMemoryDatabase($"sd-authz-cmdb-{_databaseName}"));
            services.AddDbContext<ServiceDeskDbContext>(options =>
                options.UseInMemoryDatabase($"sd-authz-sd-{_databaseName}"));
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
