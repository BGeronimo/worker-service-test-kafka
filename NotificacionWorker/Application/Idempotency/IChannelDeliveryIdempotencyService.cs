namespace NotificacionWorker.Application.Idempotency;

public interface IChannelDeliveryIdempotencyService
{
    Task<ChannelDeliveryLease> TryAcquireAsync(string eventId, string channelName, CancellationToken cancellationToken = default);
    Task MarkSucceededAsync(string eventId, string channelName, string? lockToken, CancellationToken cancellationToken = default);
    Task MarkFailedAsync(string eventId, string channelName, string? lockToken, CancellationToken cancellationToken = default);
}
