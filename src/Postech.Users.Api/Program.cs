using Microsoft.Net.Http.Headers;
using postech.Users.Api.Extensions;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

#region [Logging Configuration]

// Bootstrap logger
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

// Main logger
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
builder.Services.AddJwtAuthentication(builder.Configuration);
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

