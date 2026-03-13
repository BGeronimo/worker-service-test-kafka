using Microsoft.Extensions.Options;
using NotificacionWorker.Configuration;
using System.Text.Json;

namespace NotificacionWorker.Services;

public class PushAppResolver : IPushAppResolver
{
    private readonly ILogger<PushAppResolver> _logger;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly PushRoutingSettings _settings;

    public PushAppResolver(
        ILogger<PushAppResolver> logger,
        IHostEnvironment hostEnvironment,
        IOptions<PushRoutingSettings> options)
    {
        _logger = logger;
        _hostEnvironment = hostEnvironment;
        _settings = options.Value;
    }

    public PushAppResolution ResolveForEvent(string eventType)
    {
        var appId = _settings.EventAppMappings.TryGetValue(eventType, out var mappedAppId) && !string.IsNullOrWhiteSpace(mappedAppId)
            ? mappedAppId
            : _settings.DefaultAppId;

        if (!_settings.FirebaseApps.TryGetValue(appId, out var appSettings))
        {
            _logger.LogWarning("No se encontró configuración Firebase para AppId {AppId}. Se usará resolución vacía.", appId);
            return new PushAppResolution(appId, "Unknown", string.Empty, "No configuration found");
        }

        var normalizedLocation = NormalizeLocation(appSettings.CredentialsSource, appSettings.CredentialsLocation);
        var summary = BuildCredentialsSummary(appSettings.CredentialsSource, normalizedLocation);

        return new PushAppResolution(appId, appSettings.CredentialsSource, normalizedLocation, summary);
    }

    private string NormalizeLocation(string source, string location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return string.Empty;
        }

        if (!source.Equals("File", StringComparison.OrdinalIgnoreCase))
        {
            return location;
        }

        if (Path.IsPathRooted(location))
        {
            return location;
        }

        return Path.Combine(_hostEnvironment.ContentRootPath, location);
    }

    private string BuildCredentialsSummary(string source, string normalizedLocation)
    {
        if (!source.Equals("File", StringComparison.OrdinalIgnoreCase))
        {
            return $"Source={source}";
        }

        if (string.IsNullOrWhiteSpace(normalizedLocation))
        {
            return "File path not configured";
        }

        if (!File.Exists(normalizedLocation))
        {
            return $"File not found: {normalizedLocation}";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(normalizedLocation));
            var root = document.RootElement;

            var projectId = root.TryGetProperty("project_id", out var projectIdProp)
                ? projectIdProp.GetString()
                : "unknown";

            var clientEmail = root.TryGetProperty("client_email", out var clientEmailProp)
                ? clientEmailProp.GetString()
                : "unknown";

            return $"project_id={projectId}; client_email={clientEmail}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo leer resumen de credenciales desde {Path}", normalizedLocation);
            return $"Unable to parse credentials file: {Path.GetFileName(normalizedLocation)}";
        }
    }
}
