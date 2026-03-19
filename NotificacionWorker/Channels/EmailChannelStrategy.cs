using Confluent.Kafka;
using Microsoft.Extensions.Options;
using NotificacionWorker.Application.Composition;
using NotificacionWorker.Application.Idempotency;
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
    private readonly INotificationDataComposerResolver _dataComposerResolver;
    private readonly IChannelDeliveryIdempotencyService _idempotencyService;
    private readonly string _topic;

    public string ChannelName => "Email";

    public EmailChannelStrategy(
        ILogger<EmailChannelStrategy> logger,
        IProducer<string, string> producer,
        IEmailTemplateRenderer templateRenderer,
        INotificationDataComposerResolver dataComposerResolver,
        IChannelDeliveryIdempotencyService idempotencyService,
        IOptions<KafkaSettings> kafkaSettings)
    {
        _logger = logger;
        _producer = producer;
        _templateRenderer = templateRenderer;
        _dataComposerResolver = dataComposerResolver;
        _idempotencyService = idempotencyService;
        _topic = kafkaSettings.Value.Topics.NotificationEmail;
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

            var notification = new NotificationMessage
            {
                EventId = request.EventId,
                EventType = request.EventType,
                Subject = $"[Email] {request.EventType}",
                Body = await _templateRenderer.RenderAsync(request.EventType, composedData, cancellationToken),
                Metadata = composedData,
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
        }
        catch (Exception ex)
        {
            await _idempotencyService.MarkFailedAsync(request.EventId, ChannelName, lockToken, cancellationToken);
            _logger.LogError(ex, "[{Channel}] Error publicando mensaje a {Topic}", ChannelName, _topic);
            throw;
        }
    }
}
