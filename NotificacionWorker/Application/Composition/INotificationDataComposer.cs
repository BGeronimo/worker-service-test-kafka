using NotificacionWorker.Models;

namespace NotificacionWorker.Application.Composition;

public interface INotificationDataComposer
{
    bool CanHandle(string eventType, string channelName);
    Task<Dictionary<string, object>> ComposeAsync(NotificationRequest request, CancellationToken cancellationToken = default);
}
