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
    private readonly IProducer<string, string> _producer;
    private readonly int _maxProcessingAttempts;
    private readonly int _maxDlqPublishAttempts;
    private readonly TimeSpan _retryBackoff;

    public EmailWorker(ILogger<EmailWorker> logger, IOptions<KafkaSettings> kafkaSettings, IProducer<string, string> producer)
    {
        _logger = logger;
        _kafkaSettings = kafkaSettings.Value;
        _producer = producer;
        _maxProcessingAttempts = Math.Clamp(_kafkaSettings.RetryPolicy.MaxProcessingAttempts, 1, 10);
        _maxDlqPublishAttempts = Math.Clamp(_kafkaSettings.RetryPolicy.MaxDlqPublishAttempts, 1, 10);
        _retryBackoff = TimeSpan.FromMilliseconds(Math.Clamp(_kafkaSettings.RetryPolicy.BackoffMilliseconds, 100, 30000));

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _kafkaSettings.BootstrapServers,
            GroupId = $"{_kafkaSettings.GroupId}-email",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false
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
                    var consumeResult = _consumer.Consume(stoppingToken);

                    var processed = await ProcessEmailNotificationWithRetryAsync(consumeResult.Message.Value, stoppingToken);

                    if (processed)
                    {
                        _consumer.Commit(consumeResult);
                        continue;
                    }

                    var movedToDlq = await PublishToDlqWithRetryAsync(consumeResult, stoppingToken);
                    if (movedToDlq)
                    {
                        _consumer.Commit(consumeResult);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
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
            }
        }
        finally
        {
            _consumer.Close();
        }
    }

    private async Task<bool> ProcessEmailNotificationWithRetryAsync(string messageValue, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= _maxProcessingAttempts; attempt++)
        {
            var processed = await ProcessEmailNotification(messageValue, cancellationToken);
            if (processed)
            {
                return true;
            }

            if (attempt < _maxProcessingAttempts)
            {
                _logger.LogWarning("[EMAIL] Reintento {Attempt}/{MaxAttempts} de procesamiento", attempt + 1, _maxProcessingAttempts);
                await Task.Delay(_retryBackoff, cancellationToken);
            }
        }

        return false;
    }

    private async Task<bool> PublishToDlqWithRetryAsync(ConsumeResult<string, string> consumeResult, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= _maxDlqPublishAttempts; attempt++)
        {
            var movedToDlq = await PublishToDlqAsync(consumeResult, cancellationToken);
            if (movedToDlq)
            {
                return true;
            }

            if (attempt < _maxDlqPublishAttempts)
            {
                _logger.LogWarning("[EMAIL] Reintento {Attempt}/{MaxAttempts} para publicar en DLQ", attempt + 1, _maxDlqPublishAttempts);
                await Task.Delay(_retryBackoff, cancellationToken);
            }
        }

        _logger.LogError("[EMAIL] Se agotaron {MaxAttempts} intentos para publicar en DLQ. El offset no se commiteará y se reintentará en el siguiente ciclo.", _maxDlqPublishAttempts);
        return false;
    }

    private async Task<bool> ProcessEmailNotification(string messageValue, CancellationToken cancellationToken)
    {
        try
        {
            var notification = JsonSerializer.Deserialize<NotificationMessage>(messageValue);

            if (notification == null)
            {
                _logger.LogWarning("No se pudo deserializar el mensaje de email");
                return false;
            }

            _logger.LogInformation("[EMAIL] Procesando - EventType: {EventType}, Subject: {Subject}", 
                notification.EventType, notification.Subject);

            await SimulateEmailSending(notification, cancellationToken);

            _logger.LogInformation("[EMAIL] Enviado exitosamente para evento: {EventType}", notification.EventType);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando notificación de email");
            return false;
        }
    }

    private async Task<bool> PublishToDlqAsync(ConsumeResult<string, string> consumeResult, CancellationToken cancellationToken)
    {
        var dlqTopic = _kafkaSettings.Topics.NotificationEmailDlq;

        var dlqPayload = new
        {
            OriginalTopic = consumeResult.Topic,
            OriginalPartition = consumeResult.Partition.Value,
            OriginalOffset = consumeResult.Offset.Value,
            FailedAtUtc = DateTime.UtcNow,
            Payload = consumeResult.Message.Value
        };

        try
        {
            var message = new Message<string, string>
            {
                Key = consumeResult.Message.Key,
                Value = JsonSerializer.Serialize(dlqPayload)
            };

            await _producer.ProduceAsync(dlqTopic, message, cancellationToken);
            _logger.LogWarning("[EMAIL] Mensaje enviado a DLQ {DlqTopic} desde {Topic} [{Partition}:{Offset}]",
                dlqTopic,
                consumeResult.Topic,
                consumeResult.Partition.Value,
                consumeResult.Offset.Value);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[EMAIL] No se pudo publicar en DLQ {DlqTopic} para {Topic} [{Partition}:{Offset}]. Se dejará sin commit para reintento.",
                dlqTopic,
                consumeResult.Topic,
                consumeResult.Partition.Value,
                consumeResult.Offset.Value);

            return false;
        }
    }

    private async Task SimulateEmailSending(NotificationMessage notification, CancellationToken cancellationToken)
    {
        await Task.Delay(500, cancellationToken);

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

