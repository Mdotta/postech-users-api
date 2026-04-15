using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

namespace postech.Users.Api.Infrastructure.Messaging;

public class SnsEventPublisher : IEventPublisher
{
    private readonly IAmazonSimpleNotificationService _snsClient;
    private readonly ILogger<SnsEventPublisher> _logger;
    private readonly string _topicArn;

    public SnsEventPublisher(IAmazonSimpleNotificationService snsClient,
        ILogger<SnsEventPublisher> logger, IConfiguration configuration)
    {
        _snsClient = snsClient;
        _logger = logger;
        _topicArn = configuration["AWS:SnsTopicArn"]
                    ?? throw new InvalidOperationException("AWS SNS Topic ARN not configured");
    }

    public async Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
    {
        _logger.LogInformation("Publishing event {EventType} to SNS", typeof(T).Name);
        var request = new PublishRequest
        {
            TopicArn = _topicArn,
            Message = JsonSerializer.Serialize(message),
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                ["EventType"] = new() { DataType = "String", StringValue = typeof(T).Name }
            }
        };
        await _snsClient.PublishAsync(request, cancellationToken);
        _logger.LogInformation("Event {EventType} published to SNS", typeof(T).Name);
    }
}