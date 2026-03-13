namespace NotificacionWorker.Services;

public interface IPushAppResolver
{
    PushAppResolution ResolveForEvent(string eventType);
}

public sealed record PushAppResolution(string AppId, string CredentialsSource, string CredentialsLocation, string CredentialsSummary);
