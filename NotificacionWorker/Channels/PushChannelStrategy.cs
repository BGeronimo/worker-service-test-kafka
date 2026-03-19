using Confluent.Kafka;
using Microsoft.Extensions.Options;
using NotificacionWorker.Application.Composition;
using NotificacionWorker.Application.Idempotency;
using NotificacionWorker.Configuration;
using NotificacionWorker.Models;
using NotificacionWorker.Services;
using System.Text.Json;

namespace NotificacionWorker.Channels;

public class PushChannelStrategy : IChannelStrategy
{
    private readonly ILogger<PushChannelStrategy> _logger;
    private readonly IProducer<string, string> _producer;
    private readonly INotificationDataComposerResolver _dataComposerResolver;
    private readonly IChannelDeliveryIdempotencyService _idempotencyService;
    private readonly IPushAppResolver _pushAppResolver;
    private readonly string _topic;

    public string ChannelName => "Push";

    public PushChannelStrategy(
        ILogger<PushChannelStrategy> logger,
        IProducer<string, string> producer,
        INotificationDataComposerResolver dataComposerResolver,
        IChannelDeliveryIdempotencyService idempotencyService,
        IPushAppResolver pushAppResolver,
        IOptions<KafkaSettings> kafkaSettings)
    {
        _logger = logger;
        _producer = producer;
        _dataComposerResolver = dataComposerResolver;
        _idempotencyService = idempotencyService;
        _pushAppResolver = pushAppResolver;
        _topic = kafkaSettings.Value.Topics.NotificationPush;
    }

    public async Task ProcessAndPublishAsync(NotificationRequest request, CancellationToken cancellationToken = default)
    {
        string? lockToken = null;
        try
        {
            var lease = await _idempotencyService.TryAcquireAsync(request.EventId, ChannelName, cancellationToken);

            if (lease.Result == ChannelDeliveryAcquireResult.AlreadyProcessed)
            {
                _logger.LogInformation("[{Channel}] EventId {EventId} ya fue procesado. Se omitirá publicación duplicada.", ChannelName, request.EventId);
                return;
            }

            if (lease.Result == ChannelDeliveryAcquireResult.InProgress)
            {
                _logger.LogWarning("[{Channel}] EventId {EventId} está en procesamiento por otra instancia. Se omite este intento.", ChannelName, request.EventId);
                return;
            }

            lockToken = lease.LockToken;
            var composedData = await _dataComposerResolver.ComposeAsync(request, ChannelName, cancellationToken);
            var appResolution = _pushAppResolver.ResolveForEvent(request.EventType);

            var metadata = new Dictionary<string, object>(composedData, StringComparer.OrdinalIgnoreCase)
            {
                ["pushAppId"] = appResolution.AppId,
                ["pushCredentialsSource"] = appResolution.CredentialsSource,
                ["pushCredentialsLocation"] = appResolution.CredentialsLocation,
                ["pushCredentialsSummary"] = appResolution.CredentialsSummary
            };

            var notification = new NotificationMessage
            {
                EventId = request.EventId,
                EventType = request.EventType,
                Subject = $"[Push] {request.EventType}",
                Body = JsonSerializer.Serialize(composedData),
                Metadata = metadata,
                Timestamp = request.Timestamp
            };

            var jsonMessage = JsonSerializer.Serialize(notification);

            var result = await _producer.ProduceAsync(
                _topic,
                new Message<string, string>
                {
                    Key = notification.EventId,
                    Value = jsonMessage
                },
                cancellationToken);

            await _idempotencyService.MarkSucceededAsync(request.EventId, ChannelName, lockToken, cancellationToken);

            _logger.LogInformation("[{Channel}] Mensaje publicado a {Topic}: {Status}",
                ChannelName, _topic, result.Status);

            _logger.LogInformation("[{Channel}] Evento {EventType} resuelto a Firebase AppId {AppId} ({Source})",
                ChannelName, request.EventType, appResolution.AppId, appResolution.CredentialsSource);
        }
        catch (Exception ex)
        {
            await _idempotencyService.MarkFailedAsync(request.EventId, ChannelName, lockToken, cancellationToken);
            _logger.LogError(ex, "[{Channel}] Error publicando mensaje a {Topic}", ChannelName, _topic);
            throw;
        }
    }
}
