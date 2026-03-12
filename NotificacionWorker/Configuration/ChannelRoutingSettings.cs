namespace NotificacionWorker.Configuration;

public class ChannelRoutingSettings
{
    public Dictionary<string, List<string>> EventChannelMappings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
