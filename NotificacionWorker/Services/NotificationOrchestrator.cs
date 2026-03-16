using NotificacionWorker.Channels;
using NotificacionWorker.Models;
using System.Collections.Concurrent;

namespace NotificacionWorker.Services;

public class NotificationOrchestrator : INotificationOrchestrator
{
    private readonly ILogger<NotificationOrchestrator> _logger;
    private readonly IChannelStrategyFactory _channelFactory;

    public NotificationOrchestrator(
        ILogger<NotificationOrchestrator> logger,
        IChannelStrategyFactory channelFactory)
    {
        _logger = logger;
        _channelFactory = channelFactory;
    }

    public async Task RouteNotificationAsync(NotificationRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            _logger.LogWarning("NotificationRequest es null, no se puede procesar");
            throw new ArgumentNullException(nameof(request));
        }

        _logger.LogInformation("Orquestando notificación para evento: {EventType}", request.EventType);

        var strategies = _channelFactory.GetStrategiesForEvent(request.EventType).ToList();

        if (strategies.Count == 0)
        {
            _logger.LogWarning("No hay canales configurados para el evento: {EventType}", request.EventType);
            throw new InvalidOperationException($"No hay canales configurados para el evento: {request.EventType}");
        }

        var processingErrors = new ConcurrentQueue<Exception>();

        var tasks = strategies.Select(async strategy =>
        {
            try
            {
                await strategy.ProcessAndPublishAsync(request, cancellationToken);
                _logger.LogInformation("Notificación enviada exitosamente al canal: {Channel}", strategy.ChannelName);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando notificación al canal: {Channel}", strategy.ChannelName);
                processingErrors.Enqueue(new InvalidOperationException(
                    $"Falló el canal {strategy.ChannelName} para evento {request.EventType}",
                    ex));
            }
        });

        await Task.WhenAll(tasks);

        if (!processingErrors.IsEmpty)
        {
            throw new AggregateException("La notificación falló en uno o más canales", processingErrors);
        }

        _logger.LogInformation("Orquestación completada para evento: {EventType}", request.EventType);
    }
}
