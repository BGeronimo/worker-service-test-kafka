namespace NotificacionWorker.Configuration;

public class EmailTemplateSettings
{
    public string TemplatesRootPath { get; set; } = "Templates/Email";
    public Dictionary<string, string> EventTemplateMappings { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ordencompletada"] = "OrdenCompletada",
        ["alertainiciosesion"] = "AlertaInicioSesion"
    };
}
