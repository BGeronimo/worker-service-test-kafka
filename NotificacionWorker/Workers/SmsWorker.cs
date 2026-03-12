using Confluent.Kafka;
using NotificacionWorker.Configuration;
using NotificacionWorker.Models;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace NotificacionWorker.Workers;

public class SmsWorker : BackgroundService
{
    private readonly ILogger<SmsWorker> _logger;
    private readonly KafkaSettings _kafkaSettings;
    private readonly IConsumer<string, string> _consumer;

    public SmsWorker(ILogger<SmsWorker> logger, IOptions<KafkaSettings> kafkaSettings)
    {
        _logger = logger;
        _kafkaSettings = kafkaSettings.Value;

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _kafkaSettings.BootstrapServers,
            GroupId = $"{_kafkaSettings.GroupId}-sms",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        _consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SmsWorker iniciando...");

        _consumer.Subscribe(_kafkaSettings.Topics.NotificationSms);
        _logger.LogInformation("SmsWorker suscrito exitosamente al topic: {Topic}", _kafkaSettings.Topics.NotificationSms);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = _consumer.Consume(TimeSpan.FromSeconds(1));

                    if (consumeResult != null)
                    {
                        await ProcessSmsNotification(consumeResult.Message.Value);

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

    private async Task ProcessSmsNotification(string messageValue)
    {
        try
        {
            var notification = JsonSerializer.Deserialize<NotificationMessage>(messageValue);

            if (notification == null)
            {
                _logger.LogWarning("No se pudo deserializar el mensaje de SMS");
                return;
            }

            _logger.LogInformation("[SMS] Procesando - EventType: {EventType}, Subject: {Subject}", 
                notification.EventType, notification.Subject);

            await SimulateSmsSending(notification);

            _logger.LogInformation("[SMS] Enviado exitosamente para evento: {EventType}", notification.EventType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando notificación de SMS");
        }
    }

    private async Task SimulateSmsSending(NotificationMessage notification)
    {
        await Task.Delay(300);

        _logger.LogInformation("-------------------------------------------");
        _logger.LogInformation("[SMS SIMULADO]");
        _logger.LogInformation("Telefono: {To}", notification.To ?? "+506-1234-5678");
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

