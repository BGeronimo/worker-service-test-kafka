using NotificacionWorker.Application.Composition;
using NotificacionWorker.Models;

namespace NotificacionWorker.Features.PromocionMundialFutbol.Composers;

public class PromocionMundialFutbolSmsComposer : INotificationDataComposer
{
    public bool CanHandle(string eventType, string channelName)
    {
        return eventType.Equals("promocionmundialfutbol", StringComparison.OrdinalIgnoreCase)
            && channelName.Equals("SMS", StringComparison.OrdinalIgnoreCase);
    }

    public Task<Dictionary<string, object>> ComposeAsync(NotificationRequest request, CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>(request.Data, StringComparer.OrdinalIgnoreCase);

        var userId = NotificationCompositionDataReader.ReadString(data, "userId", "usuarioId");
        if (!string.IsNullOrWhiteSpace(userId))
        {
            data["userId"] = userId;
        }

        return Task.FromResult(data);
    }
}
