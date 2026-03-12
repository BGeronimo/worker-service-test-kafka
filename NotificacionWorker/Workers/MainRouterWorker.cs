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

    public MainRouterWorker(
        ILogger<MainRouterWorker> logger,
        IOptions<KafkaSettings> kafkaSettings,
        INotificationOrchestrator orchestrator)
    {
        _logger = logger;
        _kafkaSettings = kafkaSettings.Value;
        _orchestrator = orchestrator;

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _kafkaSettings.BootstrapServers,
            GroupId = $"{_kafkaSettings.GroupId}-router",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
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
                    var consumeResult = _consumer.Consume(TimeSpan.FromSeconds(1));

                    if (consumeResult != null)
                    {
                        _logger.LogInformation("Mensaje recibido de {Topic}", consumeResult.Topic);

                        await ProcessAndRouteMessage(consumeResult.Message.Value, stoppingToken);

                        _consumer.Commit(consumeResult);
                    }
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

    private async Task ProcessAndRouteMessage(string messageValue, CancellationToken stoppingToken)
    {
        try
        {
            var request = JsonSerializer.Deserialize<NotificationRequest>(messageValue);

            if (request == null)
            {
                _logger.LogWarning("No se pudo deserializar el mensaje");
                return;
            }

            _logger.LogInformation("Procesando evento tipo: {EventType}", request.EventType);

            await _orchestrator.RouteNotificationAsync(request, stoppingToken);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error deserializando mensaje JSON");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando mensaje");
        }
    }

    public override void Dispose()
    {
        _consumer?.Dispose();
        base.Dispose();
    }
}

