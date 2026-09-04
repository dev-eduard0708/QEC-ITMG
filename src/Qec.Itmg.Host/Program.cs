using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Hangfire;
using Hangfire.SqlServer;
using Qec.Itmg.BuildingBlocks.Email;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.Host;
using Qec.Itmg.Host.Cmdb;
using Qec.Itmg.Host.Email;
using Qec.Itmg.Host.Lookups;
using Qec.Itmg.Host.Notifications;
using Qec.Itmg.Host.Persistence;
using Qec.Itmg.Host.ServiceDesk;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Identity.Admin;
using Qec.Itmg.Identity.Authentication;
using Qec.Itmg.Identity.CurrentUser;
using Qec.Itmg.Identity.Persistence;
using Qec.Itmg.Identity.Seed;
using Qec.Itmg.Cmdb.Persistence;
using Qec.Itmg.Organization.Persistence;
using Qec.Itmg.Platform.Integrations;
using Qec.Itmg.Platform.Persistence;
using Qec.Itmg.ServiceDesk.Persistence;
using Serilog;
using System.Security.Claims;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting QEC ITMG host");

    var builder = WebApplication.CreateBuilder(args);

    builder.Configuration.AddJsonFile(
        $"appsettings.{builder.Environment.EnvironmentName}.local.json",
        optional: true,
        reloadOnChange: true);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.AddQecModules();
    builder.Services.AddIdentityAuthentication(builder.Configuration, builder.Environment);
    builder.Services.AddIdentitySeed(builder.Configuration);
    builder.Services.AddCmdbSeed();
    builder.Services.AddServiceDeskSeed();
    builder.Services.AddScoped<ISharedDbTransaction, SharedSqlTransaction>();
    builder.Services.AddScoped<TicketNotificationService>();

    bool enableHangfire = !builder.Environment.IsEnvironment("Testing");
    string? hangfireConnection = builder.Configuration.GetConnectionString(QecEfConventions.ConnectionStringName);
    if (enableHangfire && !string.IsNullOrWhiteSpace(hangfireConnection))
    {
        builder.Services.AddHangfire(configuration => configuration
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(
                hangfireConnection,
                new SqlServerStorageOptions
                {
                    SchemaName = "hangfire",
                    PrepareSchemaIfNecessary = true,
                    CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                    SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                    QueuePollInterval = TimeSpan.Zero,
                    UseRecommendedIsolationLevel = true,
                    DisableGlobalLocks = true,
                }));

        builder.Services.AddHangfireServer();
        builder.Services.AddTransient<NotificationEmailJob>();
        builder.Services.AddTransient<SlaBreachDetectionJob>();
        builder.Services.RemoveAll<IEmailQueue>();
        builder.Services.AddSingleton<IEmailQueue, HangfireEmailQueue>();
    }

    builder.Services
        .AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live", "ready"])
        .AddDbContextCheck<IdentityDbContext>("sql-identity", tags: ["ready"])
        .AddDbContextCheck<OrganizationDbContext>("sql-organization", tags: ["ready"])
        .AddDbContextCheck<PlatformDbContext>("sql-platform", tags: ["ready"])
        .AddDbContextCheck<CmdbDbContext>("sql-cmdb", tags: ["ready"])
        .AddDbContextCheck<ServiceDeskDbContext>("sql-service-desk", tags: ["ready"]);

    var app = builder.Build();

    await app.RunIdentitySeedAsync();
    await app.RunCmdbSeedAsync();
    await app.RunServiceDeskSeedAsync();

    if (enableHangfire)
    {
        IRecurringJobManager recurringJobs = app.Services.GetRequiredService<IRecurringJobManager>();
        recurringJobs.AddOrUpdate<SlaBreachDetectionJob>(
            "sla-breach-detection",
            job => job.ExecuteAsync(CancellationToken.None),
            "*/5 * * * *");
    }

    app.UseSerilogRequestLogging();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("live"),
    });

    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
    });

    app.MapIdentityAuthEndpoints();
    if (app.Environment.IsDevelopment())
    {
        app.MapDevelopmentLoginEndpoints();
    }

    app.MapCurrentUserEndpoints();
    app.MapMeNotificationEndpoints();
    app.MapMeEquipmentEndpoints();
    app.MapMeTicketEndpoints();
    app.MapTicketCollaborationEndpoints();
    app.MapIdentityAdminEndpoints();
    app.MapLookupAdminEndpoints();
    app.MapCmdbEndpoints();
    app.MapAssetEndpoints();
    app.MapTicketEndpoints();
    app.MapIncidentEndpoints();
    app.MapKnowledgeBaseEndpoints();
    app.MapIntegrationReadinessEndpoints();

    if (app.Environment.IsEnvironment("Testing"))
    {
        app.MapGet("/__test__/signin", async (HttpContext httpContext) =>
        {
            string externalId = httpContext.Request.Query["externalId"].FirstOrDefault() ?? "oid-1";
            string upn = httpContext.Request.Query["upn"].FirstOrDefault() ?? "test@qehc.edu.sa";
            string name = httpContext.Request.Query["name"].FirstOrDefault() ?? "Test User";

            ClaimsPrincipal principal = new(new ClaimsIdentity(
                [
                    new Claim(OidcPrincipalMapper.ExternalIdClaimType, externalId),
                    new Claim(ClaimTypes.NameIdentifier, externalId),
                    new Claim(OidcPrincipalMapper.UpnClaimType, upn),
                    new Claim(ClaimTypes.Upn, upn),
                    new Claim(ClaimTypes.Name, name),
                ],
                IdentityAuthenticationExtensions.CookieScheme));

            await httpContext.SignInAsync(IdentityAuthenticationExtensions.CookieScheme, principal);
            return Results.NoContent();
        });

        app.MapGet("/__test__/signin-break-glass", async (HttpContext httpContext) =>
        {
            string externalId = httpContext.Request.Query["externalId"].FirstOrDefault() ?? "break-glass:test";
            string upn = httpContext.Request.Query["upn"].FirstOrDefault() ?? "breakglass@qehc.edu.sa";
            string name = httpContext.Request.Query["name"].FirstOrDefault() ?? "Break Glass";

            ClaimsPrincipal principal = new(new ClaimsIdentity(
                [
                    new Claim(OidcPrincipalMapper.ExternalIdClaimType, externalId),
                    new Claim(ClaimTypes.NameIdentifier, externalId),
                    new Claim(OidcPrincipalMapper.UpnClaimType, upn),
                    new Claim(ClaimTypes.Upn, upn),
                    new Claim(ClaimTypes.Name, name),
                    new Claim(BreakGlassPrincipalFactory.AuthMethodClaimType, BreakGlassPrincipalFactory.AuthMethodBreakGlass),
                    new Claim(ClaimTypes.AuthenticationMethod, BreakGlassPrincipalFactory.AuthMethodBreakGlass),
                ],
                IdentityAuthenticationExtensions.CookieScheme));

            await httpContext.SignInAsync(IdentityAuthenticationExtensions.CookieScheme, principal);
            return Results.NoContent();
        });

        app.MapGet("/__test__/auth-state", async (HttpContext httpContext) =>
        {
            AuthenticateResult result =
                await httpContext.AuthenticateAsync(IdentityAuthenticationExtensions.CookieScheme);
            return Results.Text(result.Succeeded ? "authenticated" : "anonymous");
        });
    }

    Log.Information(
        "QEC ITMG host started. Environment={Environment}",
        app.Environment.EnvironmentName);

    app.Run();
}
catch (Exception exception)
{
    Log.Fatal(exception, "QEC ITMG host terminated unexpectedly");
    throw;
}
finally
{
    Log.Information("QEC ITMG host shutting down");
    Log.CloseAndFlush();
}

public partial class Program;
