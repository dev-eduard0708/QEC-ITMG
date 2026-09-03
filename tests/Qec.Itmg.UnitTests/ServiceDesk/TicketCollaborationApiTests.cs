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
using Qec.Itmg.Platform.Domain;
using Qec.Itmg.Platform.Persistence;
using Qec.Itmg.ServiceDesk.Domain;
using Qec.Itmg.ServiceDesk.Persistence;
using Qec.Itmg.ServiceDesk.Services;
using Xunit;

namespace Qec.Itmg.UnitTests.ServiceDesk;

[Collection(WebApplicationFactoryCollection.Name)]
public sealed class TicketCollaborationApiTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 2, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task Employee_OwnTicketDetail_Allowed_OtherTicket_Denied()
    {
        await using TicketCollaborationWebApplicationFactory factory = new();
        using HttpClient owner = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        using HttpClient other = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

        Guid ownerId = await SeedAndSignInAsync(factory, owner, []);
        _ = await SeedAndSignInAsync(factory, other, []);

        Guid ticketId = await CreateTicketAsync(factory, ownerId, "Own request");

        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/v1/me/tickets/{ticketId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/me/tickets/{ticketId}")).StatusCode);
    }

    [Fact]
    public async Task Employee_Comments_ExcludeInternal()
    {
        await using TicketCollaborationWebApplicationFactory factory = new();
        using HttpClient employee = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        using HttpClient it = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

        Guid employeeId = await SeedAndSignInAsync(factory, employee, []);
        _ = await SeedAndSignInAsync(factory, it, ["tickets.read", "tickets.manage"]);

        Guid ticketId = await CreateTicketAsync(factory, employeeId, "Comment filter");

        Assert.Equal(
            HttpStatusCode.Created,
            (await it.PostAsJsonAsync(
                $"/api/v1/tickets/{ticketId}/comments",
                new { body = "Internal note", visibility = "Internal" })).StatusCode);
        Assert.Equal(
            HttpStatusCode.Created,
            (await it.PostAsJsonAsync(
                $"/api/v1/tickets/{ticketId}/comments",
                new { body = "Visible note", visibility = "EmployeeVisible" })).StatusCode);

        HttpResponseMessage employeeComments = await employee.GetAsync($"/api/v1/me/tickets/{ticketId}/comments");
        Assert.Equal(HttpStatusCode.OK, employeeComments.StatusCode);
        List<CommentDto>? comments = await employeeComments.Content.ReadFromJsonAsync<List<CommentDto>>(JsonOptions);
        Assert.NotNull(comments);
        Assert.DoesNotContain(comments!, item => item.Visibility == "Internal");
        Assert.Contains(comments!, item => item.Body == "Visible note");
    }

    [Fact]
    public async Task Attachment_Endpoint_RespectsTicketOwnership()
    {
        await using TicketCollaborationWebApplicationFactory factory = new();
        using HttpClient owner = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        using HttpClient other = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

        Guid ownerId = await SeedAndSignInAsync(factory, owner, []);
        _ = await SeedAndSignInAsync(factory, other, []);

        Guid ticketId = await CreateTicketAsync(factory, ownerId, "Attachment ownership");

        using MultipartFormDataContent form = new();
        ByteArrayContent file = new(Encoding.UTF8.GetBytes("hello attachment"));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", "note.txt");

        HttpResponseMessage uploaded = await owner.PostAsync($"/api/v1/me/tickets/{ticketId}/attachments", form);
        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        AttachmentDto? attachment = await uploaded.Content.ReadFromJsonAsync<AttachmentDto>(JsonOptions);
        Assert.NotNull(attachment);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await other.GetAsync($"/api/v1/me/tickets/{ticketId}/attachments")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await other.GetAsync(
                $"/api/v1/me/tickets/{ticketId}/attachments/{attachment!.Id}/content")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await owner.GetAsync(
                $"/api/v1/me/tickets/{ticketId}/attachments/{attachment.Id}/content")).StatusCode);
    }

    [Fact]
    public async Task AssignmentAndStatus_CreateNotifications()
    {
        await using TicketCollaborationWebApplicationFactory factory = new();
        using HttpClient it = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

        Guid assigneeId = await SeedAndSignInAsync(factory, it, ["tickets.read", "tickets.manage"]);
        Guid requesterId;
        Guid ticketId;
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            IdentityDbContext identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            User requester = User.Create(
                $"{Guid.NewGuid():N}@qehc.edu.sa",
                "Requester",
                UserType.Employee,
                Now,
                directoryObjectId: $"oid-{Guid.NewGuid():N}");
            identity.Users.Add(requester);
            await identity.SaveChangesAsync();
            requesterId = requester.Id;

            TicketService tickets = scope.ServiceProvider.GetRequiredService<TicketService>();
            Ticket ticket = await tickets.CreateAsync(
                TicketType.ServiceRequest,
                "Notify me",
                "Assignment notification",
                requesterId);
            ticketId = ticket.Id;
        }

        HttpResponseMessage assigned = await it.PostAsJsonAsync(
            $"/api/v1/tickets/{ticketId}/assign",
            new { assignedUserId = assigneeId, queueId = (Guid?)null, notes = "take" });
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);

        TicketDto? afterAssign = await assigned.Content.ReadFromJsonAsync<TicketDto>(JsonOptions);
        Assert.NotNull(afterAssign);

        HttpResponseMessage status = await it.PostAsJsonAsync(
            $"/api/v1/tickets/{ticketId}/status",
            new { status = "InProgress", rowVersion = afterAssign!.RowVersion });
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);

        using IServiceScope verifyScope = factory.Services.CreateScope();
        PlatformDbContext platform = verifyScope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.True(await platform.Notifications.AnyAsync(item =>
            item.RecipientUserId == assigneeId
            && item.Type == "ticket.assigned"
            && item.ResourceId == ticketId));
        Assert.True(await platform.Notifications.AnyAsync(item =>
            item.RecipientUserId == requesterId
            && item.ResourceId == ticketId
            && (item.Type == "ticket.status_changed" || item.Type == "ticket.resolved")));
    }

    private static async Task<Guid> CreateTicketAsync(
        TicketCollaborationWebApplicationFactory factory,
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
        TicketCollaborationWebApplicationFactory factory,
        HttpClient client,
        string[] permissionKeys)
    {
        string externalId = $"oid-{Guid.NewGuid():N}";
        string upn = $"{Guid.NewGuid():N}@qehc.edu.sa";
        Guid userId;

        using (IServiceScope scope = factory.Services.CreateScope())
        {
            IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            User user = User.Create(upn, "Ticket Collab Tester", UserType.Employee, Now, directoryObjectId: externalId);
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

    private sealed record CommentDto(string Body, string Visibility);

    private sealed record AttachmentDto(Guid Id);
}

internal sealed class TicketCollaborationWebApplicationFactory : WebApplicationFactory<Program>
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
                options.UseInMemoryDatabase($"sd-collab-identity-{_databaseName}"));
            services.AddDbContext<OrganizationDbContext>(options =>
                options.UseInMemoryDatabase($"sd-collab-organization-{_databaseName}"));
            services.AddDbContext<PlatformDbContext>(options =>
                options.UseInMemoryDatabase($"sd-collab-platform-{_databaseName}"));
            services.AddDbContext<CmdbDbContext>(options =>
                options.UseInMemoryDatabase($"sd-collab-cmdb-{_databaseName}"));
            services.AddDbContext<ServiceDeskDbContext>(options =>
                options.UseInMemoryDatabase($"sd-collab-sd-{_databaseName}"));
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
