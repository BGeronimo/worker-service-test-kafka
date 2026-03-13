using Confluent.Kafka;
using Microsoft.Extensions.Options;
using NotificacionWorker.Configuration;
using NotificacionWorker.Models;
using NotificacionWorker.Services;
using System.Text.Json;

namespace NotificacionWorker.Channels;

public class PushChannelStrategy : IChannelStrategy
{
    private readonly ILogger<PushChannelStrategy> _logger;
    private readonly IProducer<string, string> _producer;
    private readonly IPushAppResolver _pushAppResolver;
    private readonly string _topic;

    public string ChannelName => "Push";

    public PushChannelStrategy(
        ILogger<PushChannelStrategy> logger,
        IProducer<string, string> producer,
        IPushAppResolver pushAppResolver,
        IOptions<KafkaSettings> kafkaSettings)
    {
        _logger = logger;
        _producer = producer;
        _pushAppResolver = pushAppResolver;
        _topic = kafkaSettings.Value.Topics.NotificationPush;
    }

    public async Task ProcessAndPublishAsync(NotificationRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var appResolution = _pushAppResolver.ResolveForEvent(request.EventType);

            var metadata = new Dictionary<string, object>(request.Data, StringComparer.OrdinalIgnoreCase)
            {
                ["pushAppId"] = appResolution.AppId,
                ["pushCredentialsSource"] = appResolution.CredentialsSource,
                ["pushCredentialsLocation"] = appResolution.CredentialsLocation,
                ["pushCredentialsSummary"] = appResolution.CredentialsSummary
            };

            var notification = new NotificationMessage
            {
                EventType = request.EventType,
                Subject = $"[Push] {request.EventType}",
                Body = JsonSerializer.Serialize(request.Data),
                Metadata = metadata,
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

            _logger.LogInformation("[{Channel}] Evento {EventType} resuelto a Firebase AppId {AppId} ({Source})",
                ChannelName, request.EventType, appResolution.AppId, appResolution.CredentialsSource);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex, "[{Channel}] Error publicando mensaje a {Topic}", ChannelName, _topic);
            throw;
        }
    }
}
