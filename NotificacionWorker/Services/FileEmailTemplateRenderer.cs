using Microsoft.Extensions.Options;
using NotificacionWorker.Configuration;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NotificacionWorker.Services;

public partial class FileEmailTemplateRenderer : IEmailTemplateRenderer
{
    private readonly ILogger<FileEmailTemplateRenderer> _logger;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly EmailTemplateSettings _settings;

    public FileEmailTemplateRenderer(
        ILogger<FileEmailTemplateRenderer> logger,
        IHostEnvironment hostEnvironment,
        IOptions<EmailTemplateSettings> options)
    {
        _logger = logger;
        _hostEnvironment = hostEnvironment;
        _settings = options.Value;
    }

    public async Task<string> RenderAsync(string eventType, Dictionary<string, object> data, CancellationToken cancellationToken = default)
    {
        var templateKey = ResolveTemplateKey(eventType);
        var templatePath = Path.Combine(
            _hostEnvironment.ContentRootPath,
            _settings.TemplatesRootPath,
            EnsureTemplateFileName(templateKey));

        if (!File.Exists(templatePath))
        {
            _logger.LogWarning("No se encontró template para evento {EventType} en ruta {TemplatePath}", eventType, templatePath);
            return BuildFallbackHtml(eventType, data);
        }

        var template = await File.ReadAllTextAsync(templatePath, cancellationToken);
        return RenderTemplate(template, data);
    }

    private string ResolveTemplateKey(string eventType)
    {
        if (_settings.EventTemplateMappings.TryGetValue(eventType, out var configuredTemplate) && !string.IsNullOrWhiteSpace(configuredTemplate))
        {
            return configuredTemplate;
        }

        return eventType;
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

    private static string BuildFallbackHtml(string eventType, Dictionary<string, object> data)
    {
        var body = string.Join(Environment.NewLine, data.Select(kvp =>
            $"<li><strong>{WebUtility.HtmlEncode(kvp.Key)}:</strong> {WebUtility.HtmlEncode(ConvertValueToString(kvp.Value))}</li>"));

        return $"<html><body><h2>{WebUtility.HtmlEncode(eventType)}</h2><ul>{body}</ul></body></html>";
    }

    [GeneratedRegex("{{\\s*[^{}]+\\s*}}", RegexOptions.Compiled)]
    private static partial Regex PlaceholderRegex();
}
