using NotificacionWorker.Channels;
using NotificacionWorker.Models;

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
            return;
        }

        _logger.LogInformation("Orquestando notificación para evento: {EventType}", request.EventType);

        var strategies = _channelFactory.GetStrategiesForEvent(request.EventType);

        if (!strategies.Any())
        {
            _logger.LogWarning("No hay canales configurados para el evento: {EventType}", request.EventType);
            return;
        }

        var tasks = strategies.Select(async strategy =>
        {
            try
            {
                await strategy.ProcessAndPublishAsync(request, cancellationToken);
                _logger.LogInformation("Notificación enviada exitosamente al canal: {Channel}", strategy.ChannelName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando notificación al canal: {Channel}", strategy.ChannelName);
            }
        });

        await Task.WhenAll(tasks);

        _logger.LogInformation("Orquestación completada para evento: {EventType}", request.EventType);
    }
}
