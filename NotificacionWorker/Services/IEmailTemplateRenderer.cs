namespace NotificacionWorker.Services;

public interface IEmailTemplateRenderer
{
    Task<string> RenderAsync(string eventType, Dictionary<string, object> data, CancellationToken cancellationToken = default);
}
