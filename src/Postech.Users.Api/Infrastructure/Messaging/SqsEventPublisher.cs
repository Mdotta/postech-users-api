using MassTransit;
using postech.Users.Api.Application.Utils;

namespace postech.Users.Api.Infrastructure.Messaging;

public class SqsEventPublisher : IEventPublisher
{
    private readonly ILogger<SqsEventPublisher> _logger;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ICorrelationContext _correlationContext;

    public SqsEventPublisher(
        IPublishEndpoint publishEndpoint,
        ILogger<SqsEventPublisher> logger,
        ICorrelationContext correlationContext)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
        _correlationContext = correlationContext;
    }

    public async Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            _logger.LogInformation("Publishing event {EventType} via MassTransit", typeof(T).Name);

            await _publishEndpoint.Publish(message, context =>
            {
                context.CorrelationId = _correlationContext.CorrelationId;
            }, cancellationToken);

            _logger.LogInformation("Event {EventType} successfully published via MassTransit", typeof(T).Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event {EventType} via MassTransit", typeof(T).Name);
            throw;
        }
    }
}
