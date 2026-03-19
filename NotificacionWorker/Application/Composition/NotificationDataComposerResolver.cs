using NotificacionWorker.Models;

namespace NotificacionWorker.Application.Composition;

public class NotificationDataComposerResolver : INotificationDataComposerResolver
{
    private readonly ILogger<NotificationDataComposerResolver> _logger;
    private readonly IReadOnlyList<INotificationDataComposer> _composers;

    public NotificationDataComposerResolver(
        ILogger<NotificationDataComposerResolver> logger,
        IEnumerable<INotificationDataComposer> composers)
    {
        _logger = logger;
        _composers = composers.ToList();
    }

    public async Task<Dictionary<string, object>> ComposeAsync(NotificationRequest request, string channelName, CancellationToken cancellationToken = default)
    {
        var composer = _composers.FirstOrDefault(c => c.CanHandle(request.EventType, channelName));

        if (composer is null)
        {
            var availableComposers = string.Join(", ", _composers.Select(c => c.GetType().Name));

            _logger.LogError(
                "No se encontró compositor para evento {EventType} y canal {Channel}. Compositores registrados: {Composers}",
                request.EventType,
                channelName,
                string.IsNullOrWhiteSpace(availableComposers) ? "(ninguno)" : availableComposers);

            throw new InvalidOperationException(
                $"No se encontró un compositor para EventType '{request.EventType}' y canal '{channelName}'.");
        }

        _logger.LogInformation("Usando compositor {Composer} para evento {EventType} y canal {Channel}",
            composer.GetType().Name,
            request.EventType,
            channelName);

        var data = await composer.ComposeAsync(request, cancellationToken);
        return data ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }
}
