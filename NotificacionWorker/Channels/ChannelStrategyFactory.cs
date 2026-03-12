using Microsoft.Extensions.Options;
using NotificacionWorker.Configuration;

namespace NotificacionWorker.Channels;

public class ChannelStrategyFactory : IChannelStrategyFactory
{
    private readonly ILogger<ChannelStrategyFactory> _logger;
    private readonly IEnumerable<IChannelStrategy> _strategies;
    private readonly Dictionary<string, List<string>> _eventChannelMappings;

    public ChannelStrategyFactory(
        ILogger<ChannelStrategyFactory> logger,
        IEnumerable<IChannelStrategy> strategies,
        IOptions<ChannelRoutingSettings> channelRoutingOptions)
    {
        _logger = logger;
        _strategies = strategies;

        var configuredMappings = channelRoutingOptions.Value.EventChannelMappings ?? new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        _eventChannelMappings = new Dictionary<string, List<string>>(configuredMappings, StringComparer.OrdinalIgnoreCase);
    }

    public IChannelStrategy? GetStrategy(string channelName)
    {
        var strategy = _strategies.FirstOrDefault(s =>
            s.ChannelName.Equals(channelName, StringComparison.OrdinalIgnoreCase));

        if (strategy == null)
        {
            _logger.LogWarning("No se encontró estrategia para el canal: {ChannelName}", channelName);
        }

        return strategy;
    }

    public IEnumerable<IChannelStrategy> GetStrategiesForEvent(string eventType)
    {
        if (!_eventChannelMappings.TryGetValue(eventType, out var channelNames))
        {
            _logger.LogWarning("No hay mapeo configurado para el evento: {EventType}", eventType);
            return Enumerable.Empty<IChannelStrategy>();
        }

        var strategies = channelNames
            .Select(GetStrategy)
            .Where(s => s != null)
            .Cast<IChannelStrategy>()
            .ToList();

        if (strategies.Count == 0)
        {
            _logger.LogWarning("No se encontraron estrategias disponibles para el evento: {EventType}", eventType);
        }

        return strategies;
    }
}
