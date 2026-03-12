using NotificacionWorker.Models;

namespace NotificacionWorker.Channels;

public interface IChannelStrategy
{
    string ChannelName { get; }
    Task ProcessAndPublishAsync(NotificationRequest request, CancellationToken cancellationToken = default);
}
