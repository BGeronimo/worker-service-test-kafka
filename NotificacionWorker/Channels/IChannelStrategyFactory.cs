namespace NotificacionWorker.Channels;

public interface IChannelStrategyFactory
{
    IChannelStrategy? GetStrategy(string channelName);
    IEnumerable<IChannelStrategy> GetStrategiesForEvent(string eventType);
}
