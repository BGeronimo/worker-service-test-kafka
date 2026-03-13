using Confluent.Kafka;
using NotificacionWorker.Configuration;
using NotificacionWorker.Models;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace NotificacionWorker.Workers;

public class PushWorker : BackgroundService
{
    private readonly ILogger<PushWorker> _logger;
    private readonly KafkaSettings _kafkaSettings;
    private readonly IConsumer<string, string> _consumer;

    public PushWorker(ILogger<PushWorker> logger, IOptions<KafkaSettings> kafkaSettings)
    {
        _logger = logger;
        _kafkaSettings = kafkaSettings.Value;

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _kafkaSettings.BootstrapServers,
            GroupId = $"{_kafkaSettings.GroupId}-push",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        _consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PushWorker iniciando...");

        _consumer.Subscribe(_kafkaSettings.Topics.NotificationPush);
        _logger.LogInformation("PushWorker suscrito exitosamente al topic: {Topic}", _kafkaSettings.Topics.NotificationPush);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = _consumer.Consume(TimeSpan.FromSeconds(1));

                    if (consumeResult != null)
                    {
                        await ProcessPushNotification(consumeResult.Message.Value);

                        _consumer.Commit(consumeResult);
                    }
                }
                catch (ConsumeException ex) when (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
                {
                    _logger.LogWarning("Topic no disponible, esperando...");
                    await Task.Delay(5000, stoppingToken);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Error consumiendo mensaje de Kafka");
                    await Task.Delay(2000, stoppingToken);
                }

                await Task.Delay(100, stoppingToken);
            }
        }
        finally
        {
            _consumer.Close();
        }
    }

    private async Task ProcessPushNotification(string messageValue)
    {
        try
        {
            var notification = JsonSerializer.Deserialize<NotificationMessage>(messageValue);

            if (notification == null)
            {
                _logger.LogWarning("No se pudo deserializar el mensaje de Push");
                return;
            }

            _logger.LogInformation("[PUSH] Procesando - EventType: {EventType}, Subject: {Subject}", 
                notification.EventType, notification.Subject);

            await SimulatePushSending(notification);

            _logger.LogInformation("[PUSH] Enviado exitosamente para evento: {EventType}", notification.EventType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando notificación Push");
        }
    }

    private async Task SimulatePushSending(NotificationMessage notification)
    {
        await Task.Delay(200);

        var appId = ReadMetadataValue(notification.Metadata, "pushAppId");
        var credentialsSource = ReadMetadataValue(notification.Metadata, "pushCredentialsSource");
        var credentialsLocation = ReadMetadataValue(notification.Metadata, "pushCredentialsLocation");
        var credentialsSummary = ReadMetadataValue(notification.Metadata, "pushCredentialsSummary");

        _logger.LogInformation("-------------------------------------------");
        _logger.LogInformation("[PUSH NOTIFICATION SIMULADO]");
        _logger.LogInformation("DeviceId: {To}", notification.To ?? "device-abc-123");
        _logger.LogInformation("Titulo: {Subject}", notification.Subject);
        _logger.LogInformation("Mensaje: {Body}", notification.Body);
        _logger.LogInformation("Firebase AppId: {AppId}", string.IsNullOrWhiteSpace(appId) ? "N/A" : appId);
        _logger.LogInformation("Credentials Source: {Source}", string.IsNullOrWhiteSpace(credentialsSource) ? "N/A" : credentialsSource);
        _logger.LogInformation("Credentials Location: {Location}", string.IsNullOrWhiteSpace(credentialsLocation) ? "N/A" : credentialsLocation);
        _logger.LogInformation("Credentials Summary: {Summary}", string.IsNullOrWhiteSpace(credentialsSummary) ? "N/A" : credentialsSummary);
        _logger.LogInformation("EventType: {EventType}", notification.EventType);
        _logger.LogInformation("Timestamp: {Timestamp}", notification.Timestamp);
        _logger.LogInformation("-------------------------------------------");
    }

    private static string ReadMetadataValue(Dictionary<string, object> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var rawValue) || rawValue is null)
        {
            return string.Empty;
        }

        return rawValue switch
        {
            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.String => jsonElement.GetString() ?? string.Empty,
            JsonElement jsonElement => jsonElement.GetRawText(),
            _ => rawValue.ToString() ?? string.Empty
        };
    }

    public override void Dispose()
    {
        _consumer?.Dispose();
        base.Dispose();
    }
}

