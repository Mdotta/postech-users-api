using MassTransit;

namespace postech.Users.Api.Infrastructure.Messaging;

public class RabbitMqEventPublisher:IEventPublisher
{
    private readonly ILogger<RabbitMqEventPublisher> _logger;
    private readonly IPublishEndpoint _publishEndpoint;
    
    public RabbitMqEventPublisher(IPublishEndpoint publishEndpoint, ILogger<RabbitMqEventPublisher> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }
    
    public async Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
    {
        _logger.LogInformation("Publishing event {EventType} to RabbitMQ", typeof(T).Name);
        await _publishEndpoint.Publish(message, cancellationToken);
        _logger.LogInformation("Event {EventType} published to RabbitMQ", typeof(T).Name);
    }
}