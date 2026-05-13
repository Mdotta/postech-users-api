using Microsoft.AspNetCore.Authorization;
using postech.Users.Api.Endpoints;
using postech.Users.Api.Infrastructure.Data;
using postech.Users.Api.Middleware;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using Scalar.AspNetCore;

namespace postech.Users.Api.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        // Middleware
        app.UseMiddleware<CorrelationIdMiddleware>();

        app.UseRouting();
        app.UseHttpMetrics(options => options.AddCustomLabel("service", _ => "users-api"));

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseAuthentication();
        app.UseAuthorization();

        // Scalar 
        app.MapOpenApi();
        app.MapScalarApiReference(options =>
            options.WithOpenApiRoutePattern("../openapi/{documentName}.json"));

        app.MapMetrics("/metrics").AllowAnonymous();

        // Map Endpoints
        app.MapAuthEndpoints();
        app.MapUserEndpoints();
        app.MapHealthEndpoints();

        return app;
    }

    public static async Task<WebApplication> ApplyMigrationsAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        
        await dbContext.Database.MigrateAsync();
        
        return app;
    }
}