using NotificacionWorker.Models;

namespace NotificacionWorker.Services;

public interface INotificationOrchestrator
{
    Task RouteNotificationAsync(NotificationRequest request, CancellationToken cancellationToken = default);
}
