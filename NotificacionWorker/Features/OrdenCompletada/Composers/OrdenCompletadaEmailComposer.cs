using NotificacionWorker.Application.Composition;
using NotificacionWorker.Models;

namespace NotificacionWorker.Features.OrdenCompletada.Composers;

public class OrdenCompletadaEmailComposer : INotificationDataComposer
{
    public bool CanHandle(string eventType, string channelName)
    {
        return eventType.Equals("ordencompletada", StringComparison.OrdinalIgnoreCase)
            && channelName.Equals("Email", StringComparison.OrdinalIgnoreCase);
    }

    public Task<Dictionary<string, object>> ComposeAsync(NotificationRequest request, CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>(request.Data, StringComparer.OrdinalIgnoreCase);

        var ordenId = NotificationCompositionDataReader.ReadString(data, "ordenId", "orderId");
        var userId = NotificationCompositionDataReader.ReadString(data, "userId", "usuarioId");

        if (string.IsNullOrWhiteSpace(ordenId) || string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidOperationException("Para 'ordencompletada' se requiere al menos 'ordenId' y 'userId'.");
        }

        data["ordenId"] = ordenId;
        data["userId"] = userId;

        return Task.FromResult(data);
    }
}
