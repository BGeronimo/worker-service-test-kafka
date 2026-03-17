namespace NotificacionWorker.Configuration;

public class EmailTemplateSettings
{
    public string TemplatesRootPath { get; set; } = "Templates/Email";
    public int CacheExpirationMinutes { get; set; } = 5;
    public Dictionary<string, string> EventTemplateMappings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
