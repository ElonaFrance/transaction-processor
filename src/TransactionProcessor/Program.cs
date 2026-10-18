using Serilog;
using TransactionProcessor.Core.Extensions;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}"));

    builder.Services.AddApplicationServices();
    builder.Services.AddHostedWorkers();

    builder.Services.AddHealthChecks()
        .AddMySql(
            builder.Configuration.GetConnectionString("Database")!,
            name: "mysql",
            tags: ["ready"]);

    var app = builder.Build();

    app.MapHealthChecks("/health/liveness", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => false
    });
    app.MapHealthChecks("/health/readiness", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    });

    Log.Information("Transaction Processor starting...");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Transaction Processor terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
