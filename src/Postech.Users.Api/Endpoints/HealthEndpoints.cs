using postech.Users.Api.Application.Events;
using postech.Users.Api.Infrastructure.Messaging;

namespace postech.Users.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
            .WithName("HealthCheck")
            .WithTags("Health")
            .AllowAnonymous();

        app.MapPost("/health/rabbitmq", async (IEventPublisher eventPublisher, ILogger<Program> logger) =>
        {
            try
            {
                logger.LogInformation("Testing RabbitMQ connection...");
                
                var testEvent = new UserCreatedEvent
                {
                    UserId = Guid.NewGuid(),
                    Email = "test@example.com",
                    Name = "Test User",
                    CreatedAt = DateTime.UtcNow
                };

                await eventPublisher.PublishAsync(testEvent);
                
                logger.LogInformation("RabbitMQ test message sent successfully");
                
                return Results.Ok(new 
                { 
                    status = "success", 
                    message = "RabbitMQ connection is working",
                    timestamp = DateTime.UtcNow 
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to publish test message to RabbitMQ");
                
                return Results.Problem(
                    title: "RabbitMQ Connection Failed",
                    detail: ex.Message,
                    statusCode: 500
                );
            }
        })
        .WithName("TestRabbitMQConnection")
        .WithTags("Health")
        .AllowAnonymous();

        return app;
    }
}

