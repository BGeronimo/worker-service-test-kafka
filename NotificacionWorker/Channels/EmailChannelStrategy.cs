using Confluent.Kafka;
using Microsoft.Extensions.Options;
using NotificacionWorker.Configuration;
using NotificacionWorker.Models;
using NotificacionWorker.Services;
using System.Text.Json;

namespace NotificacionWorker.Channels;

public class EmailChannelStrategy : IChannelStrategy
{
    private readonly ILogger<EmailChannelStrategy> _logger;
    private readonly IProducer<string, string> _producer;
    private readonly IEmailTemplateRenderer _templateRenderer;
    private readonly string _topic;

    public string ChannelName => "Email";

    public EmailChannelStrategy(
        ILogger<EmailChannelStrategy> logger,
        IProducer<string, string> producer,
        IEmailTemplateRenderer templateRenderer,
        IOptions<KafkaSettings> kafkaSettings)
    {
        _logger = logger;
        _producer = producer;
        _templateRenderer = templateRenderer;
        _topic = kafkaSettings.Value.Topics.NotificationEmail;
    }

    public async Task ProcessAndPublishAsync(NotificationRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var notification = new NotificationMessage
            {
                EventType = request.EventType,
                Subject = $"[Email] {request.EventType}",
                Body = await _templateRenderer.RenderAsync(request.EventType, request.Data, cancellationToken),
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
