using postech.Users.Api.Extensions;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
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

// Add services to the container
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructure(builder.Configuration);

// Log RabbitMQ configuration
Log.Information("Configuring RabbitMQ with Host: {Host}, Port: {Port}, User: {User}, VirtualHost: {VHost}",
    builder.Configuration["RabbitMQ:Host"] ?? "localhost",
    builder.Configuration.GetValue<int>("RabbitMQ:Port", 5672),
    builder.Configuration["RabbitMQ:Username"] ?? "guest",
    builder.Configuration["RabbitMQ:VirtualHost"] ?? "/");

builder.Services.AddMessaging(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddOpenApiWithAuth();

var app = builder.Build();

// Apply migrations
await app.ApplyMigrationsAsync();

// Configure the HTTP request pipeline
app.ConfigurePipeline();

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

