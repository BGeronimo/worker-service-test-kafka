# Sistema de Notificaciones con Kafka

## 📋 Arquitectura

Este proyecto implementa un sistema de notificaciones basado en eventos usando Apache Kafka y .NET Worker Services.

### Flujo de Eventos

```
notification.request (MainRouterWorker)
    ├── OrdenCompletada → notification.email + notification.push
    ├── AlertaInicioSesion → notification.push
    └── PromocionMundialFutbol → notification.sms

notification.email → EmailWorker
notification.sms → SmsWorker
notification.push → PushWorker
```

## 🚀 Cómo ejecutar

### 1. Iniciar Kafka (Docker)

```bash
docker-compose up -d
```

Verificar que Kafka está corriendo:
- Kafka UI: http://localhost:8080
- Kafka Broker: localhost:9092

### 2. Crear los topics de Kafka

**Opción A: Usando el script PowerShell (RECOMENDADO)**

```powershell
.\CreateKafkaTopics.ps1
```

**Opción B: Manualmente desde Kafka UI**

1. Abre http://localhost:8080
2. Click en el botón morado "+ Add a Topic"
3. Crea estos 4 topics:
   - `notification.request` (3 partitions, replication 1)
   - `notification.email` (3 partitions, replication 1)
   - `notification.sms` (3 partitions, replication 1)
   - `notification.push` (3 partitions, replication 1)

**Opción C: Usando CLI de Kafka directamente**

```bash
docker exec kafka kafka-topics --create --bootstrap-server localhost:9092 --topic notification.request --partitions 3 --replication-factor 1 --if-not-exists
docker exec kafka kafka-topics --create --bootstrap-server localhost:9092 --topic notification.email --partitions 3 --replication-factor 1 --if-not-exists
docker exec kafka kafka-topics --create --bootstrap-server localhost:9092 --topic notification.sms --partitions 3 --replication-factor 1 --if-not-exists
docker exec kafka kafka-topics --create --bootstrap-server localhost:9092 --topic notification.push --partitions 3 --replication-factor 1 --if-not-exists
```

### 3. Ejecutar el Worker Service

```bash
cd NotificacionWorker
dotnet run
```

Deberías ver logs indicando que los 4 workers están activos:
```
info: MainRouterWorker suscrito exitosamente al topic: notification.request
info: EmailWorker suscrito exitosamente al topic: notification.email
info: SmsWorker suscrito exitosamente al topic: notification.sms
info: PushWorker suscrito exitosamente al topic: notification.push
```

### 4. Enviar mensajes de prueba

**Opción A: Manualmente desde Kafka UI (RECOMENDADO)**

1. Abre http://localhost:8080
2. Ve a Topics → notification.request → Produce Message
3. En el campo "Key" pon: `1` o déjalo vacío
4. En el campo "Value" pega uno de estos JSONs:

**OrdenCompletada (envía Email + Push):**
```json
{
  "eventType": "OrdenCompletada",
  "data": {
    "orderId": "ORD-12345",
    "userId": "USER-001",
    "total": 150.50,
    "items": 3
  },
  "timestamp": "2025-01-15T10:30:00Z"
}
```

**AlertaInicioSesion (envía solo Push):**
```json
{
  "eventType": "AlertaInicioSesion",
  "data": {
    "userId": "USER-001",
    "ip": "192.168.1.100",
    "device": "iPhone 14",
    "location": "San Jose, Costa Rica"
  },
  "timestamp": "2025-01-15T10:30:00Z"
}
```

**PromocionMundialFutbol (envía solo SMS):**
```json
{
  "eventType": "PromocionMundialFutbol",
  "data": {
    "promoCode": "MUNDIAL2026",
    "discount": "25%",
    "validUntil": "2026-12-31",
    "message": "Descuento especial en productos de futbol"
  },
  "timestamp": "2025-01-15T10:30:00Z"
}
```

5. Click en "Produce Message"
6. Revisa los logs de tu Worker para ver el procesamiento

**Opción B: Usando kafka-console-producer

```bash
docker exec -it kafka kafka-console-producer --bootstrap-server localhost:9092 --topic notification.request
```

Luego pega el JSON del evento.

## 📊 Topics de Kafka

| Topic | Descripción | Producer | Consumer |
|-------|-------------|----------|----------|
| `notification.request` | Punto de entrada de eventos | Apps externas | MainRouterWorker |
| `notification.email` | Notificaciones por email | MainRouterWorker | EmailWorker |
| `notification.sms` | Notificaciones por SMS | MainRouterWorker | SmsWorker |
| `notification.push` | Notificaciones push | MainRouterWorker | PushWorker |

## 🎯 Tipos de Eventos

### 1. OrdenCompletada
Se enruta a: Email + Push

```json
{
  "eventType": "OrdenCompletada",
  "data": {
    "orderId": "ORD-12345",
    "userId": "USER-001",
    "total": 150.50,
    "items": 3
  },
  "timestamp": "2024-01-15T10:30:00Z"
}
```

### 2. AlertaInicioSesion
Se enruta a: Push

```json
{
  "eventType": "AlertaInicioSesion",
  "data": {
    "userId": "USER-001",
    "ip": "192.168.1.100",
    "device": "iPhone 14",
    "location": "San José, Costa Rica"
  },
  "timestamp": "2024-01-15T10:30:00Z"
}
```

### 3. PromocionMundialFutbol
Se enruta a: SMS

```json
{
  "eventType": "PromocionMundialFutbol",
  "data": {
    "promoCode": "MUNDIAL2026",
    "discount": "25%",
    "validUntil": "2026-12-31"
  },
  "timestamp": "2024-01-15T10:30:00Z"
}
```

## 🔧 Configuración

La configuración de Kafka está en `appsettings.json`:

```json
{
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "GroupId": "notification-worker-group",
    "Topics": {
      "NotificationRequest": "notification.request",
      "NotificationEmail": "notification.email",
      "NotificationSms": "notification.sms",
      "NotificationPush": "notification.push"
    }
  }
}
```

## 📦 Dependencias

- **Confluent.Kafka** - Cliente de Kafka para .NET
- **Microsoft.Extensions.Hosting** - Infraestructura de Worker Service

## 🏗️ Estructura del Proyecto

```
NotificacionWorker/
├── Configuration/
│   └── KafkaSettings.cs          # Configuración de Kafka
├── Models/
│   ├── NotificationRequest.cs    # Modelo de entrada
│   └── NotificationMessage.cs    # Modelo de salida
├── Workers/
│   ├── MainRouterWorker.cs       # Router principal
│   ├── EmailWorker.cs            # Procesador de emails
│   ├── SmsWorker.cs              # Procesador de SMS
│   └── PushWorker.cs             # Procesador de push
├── Program.cs                     # Punto de entrada
└── appsettings.json              # Configuración
```

## 💡 Mejoras Futuras

1. **Resiliencia**: Agregar políticas de retry con Polly
2. **Dead Letter Queue**: Para mensajes que fallan
3. **Observabilidad**: Agregar métricas con Prometheus y OpenTelemetry
4. **Validación**: Usar FluentValidation para validar mensajes
5. **Integraciones Reales**: Conectar con SendGrid, Twilio, Firebase Cloud Messaging
6. **Transacciones**: Implementar outbox pattern para garantizar consistencia
7. **Seguridad**: Agregar autenticación SASL/SSL para Kafka

## Troubleshooting

### Los workers muestran "Topic no disponible, esperando..."

**Causa**: Los topics no existen todavía en Kafka.

**Solución**:
1. **Opción rápida**: Crear topics manualmente desde Kafka UI (http://localhost:8080)
   - Click en "+ Add a Topic"
   - Crear: `notification.request`, `notification.email`, `notification.sms`, `notification.push`
   - Partitions: 3, Replication Factor: 1

2. **Opción script**: Ejecutar `.\CreateKafkaTopics.ps1`

3. **Verificar topics creados**: 
   ```bash
   docker exec kafka kafka-topics --list --bootstrap-server localhost:9092
   ```

### Error conectando a Kafka

- Verifica que Kafka esté corriendo: `docker ps | Select-String kafka`
- Verifica que `localhost:9092` esté accesible
- Si usas WSL, puede que necesites usar la IP del host de Docker

### Mensajes se consumen pero no se procesan

- Revisa los logs del worker específico (Email/SMS/Push)
- Verifica el formato JSON del mensaje
- El `eventType` debe coincidir exactamente (case-insensitive)

### Ver logs de Kafka en tiempo real

```bash
docker logs -f kafka
```

### Ejemplo de logs exitosos

Cuando todo funciona correctamente deberías ver:
```
info: MainRouterWorker suscrito exitosamente al topic: notification.request
info: Mensaje recibido de notification.request
info: Procesando evento tipo: OrdenCompletada
info: OrdenCompletada enrutada a Email y Push
info: [EMAIL] Procesando - EventType: OrdenCompletada
info: [PUSH] Procesando - EventType: OrdenCompletada
```

## Notas

- Este es un proyecto de prueba/demo con simulación de envíos
- Los workers logean en consola en lugar de enviar notificaciones reales
- Kafka está configurado sin autenticación (solo para desarrollo)
- Los topics se crean automáticamente gracias a `KAFKA_AUTO_CREATE_TOPICS_ENABLE=true`
- Los logs están limpios sin emojis para mejor compatibilidad con diferentes terminales

## Archivos del Proyecto

### Workers (NotificacionWorker/Workers/)
- `MainRouterWorker.cs` - Router principal que consume de `notification.request` y enruta a los canales apropiados
- `EmailWorker.cs` - Procesa notificaciones de email desde `notification.email`
- `SmsWorker.cs` - Procesa notificaciones de SMS desde `notification.sms`
- `PushWorker.cs` - Procesa notificaciones push desde `notification.push`

### Modelos (NotificacionWorker/Models/)
- `NotificationRequest.cs` - Modelo de entrada para eventos
- `NotificationMessage.cs` - Modelo de salida para notificaciones

### Configuración
- `KafkaSettings.cs` - Configuración de Kafka
- `appsettings.json` - Configuración de la aplicación
- `Program.cs` - Punto de entrada y registro de servicios
