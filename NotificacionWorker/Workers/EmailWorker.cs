using Confluent.Kafka;
using NotificacionWorker.Configuration;
using NotificacionWorker.Models;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace NotificacionWorker.Workers;

public class EmailWorker : BackgroundService
{
    private readonly ILogger<EmailWorker> _logger;
    private readonly KafkaSettings _kafkaSettings;
    private readonly IConsumer<string, string> _consumer;

    public EmailWorker(ILogger<EmailWorker> logger, IOptions<KafkaSettings> kafkaSettings)
    {
        _logger = logger;
        _kafkaSettings = kafkaSettings.Value;

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _kafkaSettings.BootstrapServers,
            GroupId = $"{_kafkaSettings.GroupId}-email",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        _consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EmailWorker iniciando...");

        _consumer.Subscribe(_kafkaSettings.Topics.NotificationEmail);
        _logger.LogInformation("EmailWorker suscrito exitosamente al topic: {Topic}", _kafkaSettings.Topics.NotificationEmail);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = _consumer.Consume(TimeSpan.FromSeconds(1));

                    if (consumeResult != null)
                    {
                        await ProcessEmailNotification(consumeResult.Message.Value);

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

    private async Task ProcessEmailNotification(string messageValue)
    {
        try
        {
            var notification = JsonSerializer.Deserialize<NotificationMessage>(messageValue);

            if (notification == null)
            {
                _logger.LogWarning("No se pudo deserializar el mensaje de email");
                return;
            }

            _logger.LogInformation("[EMAIL] Procesando - EventType: {EventType}, Subject: {Subject}", 
                notification.EventType, notification.Subject);

            await SimulateEmailSending(notification);

            _logger.LogInformation("[EMAIL] Enviado exitosamente para evento: {EventType}", notification.EventType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando notificación de email");
        }
    }

    private async Task SimulateEmailSending(NotificationMessage notification)
    {
        await Task.Delay(500);

        _logger.LogInformation("-------------------------------------------");
        _logger.LogInformation("[EMAIL SIMULADO]");
        _logger.LogInformation("Para: {To}", notification.To ?? "usuario@ejemplo.com");
        _logger.LogInformation("Asunto: {Subject}", notification.Subject);
        _logger.LogInformation("Cuerpo: {Body}", notification.Body);
        _logger.LogInformation("Timestamp: {Timestamp}", notification.Timestamp);
        _logger.LogInformation("-------------------------------------------");
    }

    public override void Dispose()
    {
        _consumer?.Dispose();
        base.Dispose();
    }
}

