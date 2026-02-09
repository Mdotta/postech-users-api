using MassTransit;
using postech.Users.Api.Application.Utils;

namespace postech.Users.Api.Infrastructure.Messaging;

public class RabbitMqEventPublisher:IEventPublisher
{
    private readonly ILogger<RabbitMqEventPublisher> _logger;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ICorrelationContext _correlationContext;
    public RabbitMqEventPublisher(IPublishEndpoint publishEndpoint, 
        ILogger<RabbitMqEventPublisher> logger,
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
            _logger.LogInformation(
                "Publishing event {EventType} to RabbitMQ with CorrelationId {CorrelationId}", 
                typeof(T).Name, 
                _correlationContext.CorrelationId);
            
            await _publishEndpoint.Publish(message, context =>
            {
                context.CorrelationId = _correlationContext.CorrelationId;
            }, cancellationToken);
            
            _logger.LogInformation(
                "Event {EventType} successfully published to RabbitMQ with CorrelationId {CorrelationId}", 
                typeof(T).Name, 
                _correlationContext.CorrelationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to publish event {EventType} to RabbitMQ. CorrelationId: {CorrelationId}. Error: {ErrorMessage}", 
                typeof(T).Name, 
                _correlationContext.CorrelationId, 
                ex.Message);
            throw;
        }
    }
}