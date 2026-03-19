using NotificacionWorker.Models;

namespace NotificacionWorker.Application.Composition;

public interface INotificationDataComposerResolver
{
    Task<Dictionary<string, object>> ComposeAsync(NotificationRequest request, string channelName, CancellationToken cancellationToken = default);
}
