namespace NotificacionWorker.Configuration;

public class PushRoutingSettings
{
    public string DefaultAppId { get; set; } = "app-default";
    public Dictionary<string, string> EventAppMappings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, FirebaseAppSettings> FirebaseApps { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class FirebaseAppSettings
{
    public string CredentialsSource { get; set; } = "File";
    public string CredentialsLocation { get; set; } = string.Empty;
}
