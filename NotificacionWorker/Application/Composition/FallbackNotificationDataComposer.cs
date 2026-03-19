using NotificacionWorker.Models;

namespace NotificacionWorker.Application.Composition;

public class FallbackNotificationDataComposer : INotificationDataComposer
{
    public bool CanHandle(string eventType, string channelName)
    {
        return true;
    }

    public Task<Dictionary<string, object>> ComposeAsync(NotificationRequest request, CancellationToken cancellationToken = default)
    {
        var copy = new Dictionary<string, object>(request.Data, StringComparer.OrdinalIgnoreCase);
        return Task.FromResult(copy);
    }
}
