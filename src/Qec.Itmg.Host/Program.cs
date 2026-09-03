using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Qec.Itmg.Host;
using Qec.Itmg.Host.Persistence;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Identity.Admin;
using Qec.Itmg.Identity.Authentication;
using Qec.Itmg.Identity.CurrentUser;
using Qec.Itmg.Identity.Persistence;
using Qec.Itmg.Identity.Seed;
using Qec.Itmg.Organization.Persistence;
using Qec.Itmg.Platform.Integrations;
using Qec.Itmg.Platform.Persistence;
using Qec.Itmg.Host.Notifications;
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
    builder.Services.AddScoped<ISharedDbTransaction, SharedSqlTransaction>();

    builder.Services
        .AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live", "ready"])
        .AddDbContextCheck<IdentityDbContext>("sql-identity", tags: ["ready"])
        .AddDbContextCheck<OrganizationDbContext>("sql-organization", tags: ["ready"])
        .AddDbContextCheck<PlatformDbContext>("sql-platform", tags: ["ready"]);

    var app = builder.Build();

    await app.RunIdentitySeedAsync();

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
    app.MapCurrentUserEndpoints();
    app.MapMeNotificationEndpoints();
    app.MapIdentityAdminEndpoints();
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
