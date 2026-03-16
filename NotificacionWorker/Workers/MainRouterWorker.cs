using Confluent.Kafka;
using NotificacionWorker.Configuration;
using NotificacionWorker.Models;
using NotificacionWorker.Services;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace NotificacionWorker.Workers;

public class MainRouterWorker : BackgroundService
{
    private readonly ILogger<MainRouterWorker> _logger;
    private readonly KafkaSettings _kafkaSettings;
    private readonly IConsumer<string, string> _consumer;
    private readonly INotificationOrchestrator _orchestrator;
    private readonly IProducer<string, string> _producer;

    public MainRouterWorker(
        ILogger<MainRouterWorker> logger,
        IOptions<KafkaSettings> kafkaSettings,
        INotificationOrchestrator orchestrator,
        IProducer<string, string> producer)
    {
        _logger = logger;
        _kafkaSettings = kafkaSettings.Value;
        _orchestrator = orchestrator;
        _producer = producer;

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _kafkaSettings.BootstrapServers,
            GroupId = $"{_kafkaSettings.GroupId}-router",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false
        };

        _consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MainRouterWorker iniciando...");

        _consumer.Subscribe(_kafkaSettings.Topics.NotificationRequest);
        _logger.LogInformation("MainRouterWorker suscrito exitosamente al topic: {Topic}", _kafkaSettings.Topics.NotificationRequest);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = _consumer.Consume(stoppingToken);

                    _logger.LogInformation("Mensaje recibido de {Topic}", consumeResult.Topic);

                    var processed = await ProcessAndRouteMessage(consumeResult.Message.Value, stoppingToken);

                    if (processed)
                    {
                        _consumer.Commit(consumeResult);
                        continue;
                    }

                    var movedToDlq = await PublishToDlqAsync(consumeResult, stoppingToken);
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
                    _logger.LogWarning("Topic no disponible, esperando... (esto es normal al inicio)");
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

    private async Task<bool> ProcessAndRouteMessage(string messageValue, CancellationToken stoppingToken)
    {
        try
        {
            var request = JsonSerializer.Deserialize<NotificationRequest>(messageValue);

            if (request == null)
            {
                _logger.LogWarning("No se pudo deserializar el mensaje");
                return false;
            }

            _logger.LogInformation("Procesando evento tipo: {EventType}", request.EventType);

            await _orchestrator.RouteNotificationAsync(request, stoppingToken);
            return true;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error deserializando mensaje JSON");
            return false;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando mensaje");
            return false;
        }
    }

    private async Task<bool> PublishToDlqAsync(ConsumeResult<string, string> consumeResult, CancellationToken cancellationToken)
    {
        var dlqTopic = _kafkaSettings.Topics.NotificationRequestDlq;

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
            _logger.LogWarning("Mensaje enviado a DLQ {DlqTopic} desde {Topic} [{Partition}:{Offset}]",
                dlqTopic,
                consumeResult.Topic,
                consumeResult.Partition.Value,
                consumeResult.Offset.Value);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "No se pudo publicar en DLQ {DlqTopic} para {Topic} [{Partition}:{Offset}]. Se dejará sin commit para reintento.",
                dlqTopic,
                consumeResult.Topic,
                consumeResult.Partition.Value,
                consumeResult.Offset.Value);

            return false;
        }
    }

    public override void Dispose()
    {
        _consumer?.Dispose();
        base.Dispose();
    }
}

