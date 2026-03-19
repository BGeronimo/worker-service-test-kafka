namespace NotificacionWorker.Application.Idempotency;

public enum ChannelDeliveryAcquireResult
{
    Acquired,
    AlreadyProcessed,
    InProgress
}
