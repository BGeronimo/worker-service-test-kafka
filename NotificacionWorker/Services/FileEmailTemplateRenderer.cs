using Microsoft.Extensions.Options;
using NotificacionWorker.Configuration;
using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NotificacionWorker.Services;

public partial class FileEmailTemplateRenderer : IEmailTemplateRenderer
{
    private readonly ILogger<FileEmailTemplateRenderer> _logger;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly EmailTemplateSettings _settings;
    private readonly TimeSpan _cacheExpiration;
    private readonly ConcurrentDictionary<string, CachedTemplate> _templateCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _templateLocks = new(StringComparer.OrdinalIgnoreCase);

    public FileEmailTemplateRenderer(
        ILogger<FileEmailTemplateRenderer> logger,
        IHostEnvironment hostEnvironment,
        IOptions<EmailTemplateSettings> options)
    {
        _logger = logger;
        _hostEnvironment = hostEnvironment;
        _settings = options.Value;
        _cacheExpiration = TimeSpan.FromMinutes(Math.Clamp(_settings.CacheExpirationMinutes, 1, 60));
    }

    public async Task<string> RenderAsync(string eventType, Dictionary<string, object> data, CancellationToken cancellationToken = default)
    {
        var templateKey = ResolveTemplateKey(eventType);
        var templatePath = Path.Combine(
            _hostEnvironment.ContentRootPath,
            _settings.TemplatesRootPath,
            EnsureTemplateFileName(templateKey));

        var template = await GetTemplateAsync(templatePath, cancellationToken);

        if (template is null)
        {
            throw new InvalidOperationException($"No se encontró el archivo de template para el evento '{eventType}' en la ruta '{templatePath}'.");
        }

        return RenderTemplate(template, data);
    }

    private async Task<string?> GetTemplateAsync(string templatePath, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        if (_templateCache.TryGetValue(templatePath, out var cachedTemplate) && cachedTemplate.ExpiresAtUtc > now)
        {
            return cachedTemplate.Content;
        }

        var templateLock = _templateLocks.GetOrAdd(templatePath, _ => new SemaphoreSlim(1, 1));
        await templateLock.WaitAsync(cancellationToken);

        try
        {
            now = DateTimeOffset.UtcNow;

            if (_templateCache.TryGetValue(templatePath, out cachedTemplate) && cachedTemplate.ExpiresAtUtc > now)
            {
                return cachedTemplate.Content;
            }

            if (!File.Exists(templatePath))
            {
                return null;
            }

            var template = await File.ReadAllTextAsync(templatePath, cancellationToken);
            _templateCache[templatePath] = new CachedTemplate(template, now.Add(_cacheExpiration));

            return template;
        }
        finally
        {
            templateLock.Release();
        }
    }

    private string ResolveTemplateKey(string eventType)
    {
        if (_settings.EventTemplateMappings.TryGetValue(eventType, out var configuredTemplate) && !string.IsNullOrWhiteSpace(configuredTemplate))
        {
            return configuredTemplate;
        }

        throw new InvalidOperationException($"No existe un mapeo de template para el evento '{eventType}' en la configuración 'EmailTemplates:EventTemplateMappings'.");
    }

    private static string EnsureTemplateFileName(string templateKey)
    {
        return templateKey.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
            ? templateKey
            : $"{templateKey}.html";
    }

    private static string RenderTemplate(string template, Dictionary<string, object> data)
    {
        var rendered = template;

        foreach (var (key, value) in data)
        {
            var placeholder = $"{{{{{key}}}}}";
            var encodedValue = WebUtility.HtmlEncode(ConvertValueToString(value));
            rendered = rendered.Replace(placeholder, encodedValue, StringComparison.OrdinalIgnoreCase);
        }

        rendered = PlaceholderRegex().Replace(rendered, string.Empty);

        return rendered;
    }

    private static string ConvertValueToString(object? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        return value switch
        {
            JsonElement jsonElement => jsonElement.ValueKind switch
            {
                JsonValueKind.String => jsonElement.GetString() ?? string.Empty,
                JsonValueKind.Number => jsonElement.GetRawText(),
                JsonValueKind.True => bool.TrueString,
                JsonValueKind.False => bool.FalseString,
                JsonValueKind.Null => string.Empty,
                _ => jsonElement.GetRawText()
            },
            _ => Convert.ToString(value) ?? string.Empty
        };
    }

    [GeneratedRegex("{{\\s*[^{}]+\\s*}}", RegexOptions.Compiled)]
    private static partial Regex PlaceholderRegex();

    private sealed record CachedTemplate(string Content, DateTimeOffset ExpiresAtUtc);
}
