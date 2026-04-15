using DotNetEnv;
using postech.Users.Api.Extensions;
using Serilog;

// Load .env from project root if present (no-op when running in containers/CI)
Env.Load(Path.Combine(Directory.GetCurrentDirectory(), ".env"),
    new LoadOptions(setEnvVars: true, clobberExistingVars: false, onlyExactPath: false));

var builder = WebApplication.CreateBuilder(args);

#region [Logging Configuration]

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog((context, services, options) =>
{
    options
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.WithCorrelationId(headerName: "X-Correlation-Id", addValueIfHeaderAbsence: true);
});

#endregion

#region [Builder Extensions]

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMessaging(builder.Configuration);
builder.Services.AddCognitoAuthentication(builder.Configuration); // was AddJwtAuthentication
builder.Services.AddOpenApiWithAuth();

#endregion

var app = builder.Build();

#region [App Extensions]

await app.ApplyMigrationsAsync();
app.ConfigurePipeline();

#endregion

try
{
    Log.Information("Starting Users API");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}