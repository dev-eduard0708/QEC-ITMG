using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Qec.Itmg.Host;
using Qec.Itmg.Identity.Persistence;
using Qec.Itmg.Organization.Persistence;
using Qec.Itmg.Platform.Persistence;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

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
    builder.Services
        .AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live", "ready"])
        .AddDbContextCheck<IdentityDbContext>("sql-identity", tags: ["ready"])
        .AddDbContextCheck<OrganizationDbContext>("sql-organization", tags: ["ready"])
        .AddDbContextCheck<PlatformDbContext>("sql-platform", tags: ["ready"]);

    var app = builder.Build();

    app.UseSerilogRequestLogging();

    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("live"),
    });

    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
    });

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
