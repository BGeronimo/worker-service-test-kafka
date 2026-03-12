using Confluent.Kafka;
using Microsoft.Extensions.Options;
using NotificacionWorker.Configuration;
using NotificacionWorker.Models;
using System.Text.Json;

namespace NotificacionWorker.Channels;

public class PushChannelStrategy : IChannelStrategy
{
    private readonly ILogger<PushChannelStrategy> _logger;
    private readonly IProducer<string, string> _producer;
    private readonly string _topic;

    public string ChannelName => "Push";

    public PushChannelStrategy(
        ILogger<PushChannelStrategy> logger,
        IProducer<string, string> producer,
        IOptions<KafkaSettings> kafkaSettings)
    {
        _logger = logger;
        _producer = producer;
        _topic = kafkaSettings.Value.Topics.NotificationPush;
    }

    public async Task ProcessAndPublishAsync(NotificationRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var notification = new NotificationMessage
            {
                EventType = request.EventType,
                Subject = $"[Push] {request.EventType}",
                Body = JsonSerializer.Serialize(request.Data),
                Metadata = request.Data,
                Timestamp = request.Timestamp
            };

            var jsonMessage = JsonSerializer.Serialize(notification);

            var result = await _producer.ProduceAsync(
                _topic,
                new Message<string, string>
                {
                    Key = notification.EventType,
                    Value = jsonMessage
                },
                cancellationToken);

            _logger.LogInformation("[{Channel}] Mensaje publicado a {Topic}: {Status}",
                ChannelName, _topic, result.Status);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex, "[{Channel}] Error publicando mensaje a {Topic}", ChannelName, _topic);
            throw;
        }
    }
}
