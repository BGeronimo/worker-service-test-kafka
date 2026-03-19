namespace NotificacionWorker.Application.Idempotency;

public sealed record ChannelDeliveryLease(
    ChannelDeliveryAcquireResult Result,
    string? LockToken = null);
