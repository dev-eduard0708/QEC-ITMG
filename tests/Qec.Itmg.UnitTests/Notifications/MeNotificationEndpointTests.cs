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
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Identity.CurrentUser;
using Qec.Itmg.Identity.Domain;
using Qec.Itmg.Identity.Persistence;
using Qec.Itmg.Cmdb.Persistence;
using Qec.Itmg.ServiceDesk.Persistence;
using Qec.Itmg.Organization.Persistence;
using Qec.Itmg.Platform.Domain;
using Qec.Itmg.Platform.Notifications;
using Qec.Itmg.Platform.Persistence;
using Xunit;

namespace Qec.Itmg.UnitTests.Notifications;

[Collection(WebApplicationFactoryCollection.Name)]
public sealed class MeNotificationEndpointTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 19, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Notifications_Unauthenticated_Returns401()
    {
        await using NotificationWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me/notifications")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/v1/me/notifications/unread-count")).StatusCode);
    }

    [Fact]
    public async Task Notifications_ListAndMarkRead_AreOwnerScoped()
    {
        await using NotificationWebApplicationFactory factory = new();
        await SeedEmployeeRoleAsync(factory);

        using HttpClient ownerClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
        });

        string ownerExternal = $"google-owner-{Guid.NewGuid():N}";
        string ownerUpn = $"owner-{Guid.NewGuid():N}@qehc.edu.sa";
        await ownerClient.GetAsync(
            $"/__test__/signin?externalId={Uri.EscapeDataString(ownerExternal)}&upn={Uri.EscapeDataString(ownerUpn)}");

        HttpResponseMessage me = await ownerClient.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using JsonDocument meBody = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        Guid ownerId = meBody.RootElement.GetProperty("id").GetGuid();

        Guid otherUserId;
        Guid foreignNotificationId;
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            INotificationService notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
            IdentityDbContext identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

            User other = User.Create($"other-{Guid.NewGuid():N}@qehc.edu.sa", "Other", UserType.Employee, Now);
            identity.Users.Add(other);
            await identity.SaveChangesAsync();
            otherUserId = other.Id;

            await notifications.CreateAsync(
                ownerId,
                "ticket.assigned",
                NotificationSeverity.Info,
                "Yours",
                "Owner notification");

            NotificationDto foreign = await notifications.CreateAsync(
                otherUserId,
                "ticket.assigned",
                NotificationSeverity.Info,
                "Theirs",
                "Other notification");
            foreignNotificationId = foreign.Id;
        }

        HttpResponseMessage list = await ownerClient.GetAsync("/api/v1/me/notifications");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        List<JsonElement>? items = await list.Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.NotNull(items);
        Assert.Single(items!);
        Assert.Equal("Yours", items[0].GetProperty("title").GetString());

        HttpResponseMessage count = await ownerClient.GetAsync("/api/v1/me/notifications/unread-count");
        Assert.Equal(HttpStatusCode.OK, count.StatusCode);
        using JsonDocument countBody = JsonDocument.Parse(await count.Content.ReadAsStringAsync());
        Assert.Equal(1, countBody.RootElement.GetProperty("count").GetInt32());

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await ownerClient.PostAsync($"/api/v1/me/notifications/{foreignNotificationId}/read", null)).StatusCode);

        Guid ownId = items[0].GetProperty("id").GetGuid();
        HttpResponseMessage marked = await ownerClient.PostAsync($"/api/v1/me/notifications/{ownId}/read", null);
        Assert.Equal(HttpStatusCode.OK, marked.StatusCode);

        using JsonDocument markedBody = JsonDocument.Parse(await marked.Content.ReadAsStringAsync());
        Assert.True(markedBody.RootElement.GetProperty("isRead").GetBoolean());

        count = await ownerClient.GetAsync("/api/v1/me/notifications/unread-count");
        using JsonDocument countAfter = JsonDocument.Parse(await count.Content.ReadAsStringAsync());
        Assert.Equal(0, countAfter.RootElement.GetProperty("count").GetInt32());
    }

    private static async Task SeedEmployeeRoleAsync(NotificationWebApplicationFactory factory)
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

internal sealed class NotificationWebApplicationFactory : WebApplicationFactory<Program>
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
            services.RemoveAll(typeof(IClock));
            services.AddSingleton<IClock>(new FixedClock(new DateTimeOffset(2026, 9, 3, 19, 0, 0, TimeSpan.Zero)));

            RemoveDbContext<IdentityDbContext>(services);
            RemoveDbContext<OrganizationDbContext>(services);
            RemoveDbContext<PlatformDbContext>(services);
            RemoveDbContext<CmdbDbContext>(services);
            RemoveDbContext<ServiceDeskDbContext>(services);

            services.AddDbContext<IdentityDbContext>(options =>
                options.UseInMemoryDatabase($"notify-identity-{_databaseName}"));
            services.AddDbContext<OrganizationDbContext>(options =>
                options.UseInMemoryDatabase($"notify-organization-{_databaseName}"));
            services.AddDbContext<PlatformDbContext>(options =>
                options.UseInMemoryDatabase($"notify-platform-{_databaseName}"));
            services.AddDbContext<CmdbDbContext>(options =>
                options.UseInMemoryDatabase($"notify-cmdb-{_databaseName}"));
            services.AddDbContext<ServiceDeskDbContext>(options =>
                options.UseInMemoryDatabase($"notify-sd-{_databaseName}"));
        });
    }

    private static void RemoveDbContext<TContext>(IServiceCollection services)
        where TContext : DbContext
    {
        services.RemoveAll(typeof(DbContextOptions<TContext>));
        services.RemoveAll(typeof(IDbContextOptionsConfiguration<TContext>));
        services.RemoveAll(typeof(TContext));
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
