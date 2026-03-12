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

        _logger.LogInformation("-------------------------------------------");
        _logger.LogInformation("[PUSH NOTIFICATION SIMULADO]");
        _logger.LogInformation("DeviceId: {To}", notification.To ?? "device-abc-123");
        _logger.LogInformation("Titulo: {Subject}", notification.Subject);
        _logger.LogInformation("Mensaje: {Body}", notification.Body);
        _logger.LogInformation("EventType: {EventType}", notification.EventType);
        _logger.LogInformation("Timestamp: {Timestamp}", notification.Timestamp);
        _logger.LogInformation("-------------------------------------------");
    }

    public override void Dispose()
    {
        _consumer?.Dispose();
        base.Dispose();
    }
}

