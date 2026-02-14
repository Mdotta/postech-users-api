using postech.Users.Api.Extensions;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog

#region [Logging Configuration]

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "Users.Api")
    .CreateLogger();

builder.Host.UseSerilog((context, services, options) =>
{
    options
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext();
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

