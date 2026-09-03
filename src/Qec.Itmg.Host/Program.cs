using Serilog;
using Qec.Itmg.Host;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting QEC ITMG host");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.AddQecModules();

    builder.Services.AddHealthChecks();

    var app = builder.Build();

    app.UseSerilogRequestLogging();

    app.MapHealthChecks("/health/live");
    app.MapHealthChecks("/health/ready");

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
