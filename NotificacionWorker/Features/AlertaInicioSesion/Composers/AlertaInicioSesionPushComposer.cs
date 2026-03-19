using NotificacionWorker.Application.Composition;
using NotificacionWorker.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace NotificacionWorker.Features.AlertaInicioSesion.Composers
{
    public class AlertaInicioSesionPushComposer : INotificationDataComposer
    {
        public bool CanHandle(string eventType, string channelName)
        {
            return eventType.Equals("alertainiciosesion", StringComparison.OrdinalIgnoreCase)
                && channelName.Equals("Push", StringComparison.OrdinalIgnoreCase);
        }

        public Task<Dictionary<string, object>> ComposeAsync(NotificationRequest request, CancellationToken cancellationToken = default)
        {
            //generar copia de los datos para evitar modificar el original
            var data = new Dictionary<string, object>(request.Data, StringComparer.OrdinalIgnoreCase);

            //puedo hacer llamadas a API aqui
            //ejemplo: obtener detalles del usuario para personalizar la notificación

            data["location"] = "6 avenida 8-56 zona 9 pista derecha";
            data["device"] = "Google Chrome en Windows 10";

            return Task.FromResult(data);
        }
    }
}
