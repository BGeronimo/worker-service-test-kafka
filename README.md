# Sistema de Notificaciones con Kafka

Proyecto de ejemplo con `.NET Worker Service` y `Apache Kafka` para enrutar y procesar notificaciones por canal (`Email`, `SMS`, `Push`).

## Ruta recomendada para leer el código (por dónde empezar)

Si quieres entender rápido el proyecto, este es el orden sugerido:

1. `NotificacionWorker/Program.cs`
   - Registro de dependencias
   - Configuración Kafka y workers activos
2. `NotificacionWorker/Configuration/KafkaSettings.cs`
   - Topics principales + DLQ
   - Política de reintentos (`RetryPolicy`)
3. `NotificacionWorker/Workers/MainRouterWorker.cs`
   - Entrada principal (`notification.request`)
   - Flujo de commit, retry y DLQ
4. `NotificacionWorker/Services/NotificationOrchestrator.cs`
   - Lógica de orquestación por evento
5. `NotificacionWorker/Channels/ChannelStrategyFactory.cs`
   - Resolución de canales según `eventType`
6. `NotificacionWorker/Channels/*.cs`
   - Publicación a `notification.email`, `notification.sms`, `notification.push`
7. `NotificacionWorker/Workers/EmailWorker.cs`, `SmsWorker.cs`, `PushWorker.cs`
   - Consumo por canal y envío simulado
8. `NotificacionWorker/appsettings.json`
   - Mapeos de eventos, templates, push apps y retry values

## Estructura del proyecto

```text
NotificacionWorker/
├── Program.cs
├── appsettings.json
├── Configuration/
│   ├── KafkaSettings.cs
│   ├── ChannelRoutingSettings.cs
│   ├── EmailTemplateSettings.cs
│   └── PushRoutingSettings.cs
├── Models/
│   ├── NotificationRequest.cs
│   └── NotificationMessage.cs
├── Services/
│   ├── NotificationOrchestrator.cs
│   ├── FileEmailTemplateRenderer.cs
│   └── PushAppResolver.cs
├── Channels/
│   ├── ChannelStrategyFactory.cs
│   ├── EmailChannelStrategy.cs
│   ├── SmsChannelStrategy.cs
│   └── PushChannelStrategy.cs
├── Workers/
│   ├── MainRouterWorker.cs
│   ├── EmailWorker.cs
│   ├── SmsWorker.cs
│   └── PushWorker.cs
└── Credentials/
    ├── firebase-intelaf.json
    └── firebase-seguridad.json
```

## Arquitectura actual

Flujo principal:

1. `MainRouterWorker` consume de `notification.request`
2. Orquesta el enrutamiento por evento
3. Publica en topics de canal (`notification.email`, `notification.sms`, `notification.push`)
4. Cada worker de canal consume su topic y simula envío

### Resiliencia implementada

- `Producer` idempotente (`EnableIdempotence=true`, `Acks=All`)
- `EnableAutoCommit=false`
- `EnableAutoOffsetStore=false`
- Reintentos configurables para:
  - procesamiento
  - publicación a DLQ
- Patrón de commit seguro:
  - si procesa bien -> `commit`
  - si falla y se publica a DLQ -> `commit`
  - si falla DLQ -> **no commit** (reintento posterior)

## Topics requeridos

### Principales

- `notification.request`
- `notification.email`
- `notification.sms`
- `notification.push`

### DLQ

- `notification.request.dlq`
- `notification.email.dlq`
- `notification.sms.dlq`
- `notification.push.dlq`

## Ejecución local

### 1) Levantar Kafka

```bash
docker-compose up -d
```

Kafka UI: `http://localhost:8080`

### 2) Crear topics

Crear manualmente en Kafka UI o por CLI.

Ejemplo CLI:

```bash
docker exec kafka kafka-topics --create --bootstrap-server localhost:9092 --topic notification.request --partitions 3 --replication-factor 1 --if-not-exists
docker exec kafka kafka-topics --create --bootstrap-server localhost:9092 --topic notification.email --partitions 3 --replication-factor 1 --if-not-exists
docker exec kafka kafka-topics --create --bootstrap-server localhost:9092 --topic notification.sms --partitions 3 --replication-factor 1 --if-not-exists
docker exec kafka kafka-topics --create --bootstrap-server localhost:9092 --topic notification.push --partitions 3 --replication-factor 1 --if-not-exists
docker exec kafka kafka-topics --create --bootstrap-server localhost:9092 --topic notification.request.dlq --partitions 3 --replication-factor 1 --if-not-exists
docker exec kafka kafka-topics --create --bootstrap-server localhost:9092 --topic notification.email.dlq --partitions 3 --replication-factor 1 --if-not-exists
docker exec kafka kafka-topics --create --bootstrap-server localhost:9092 --topic notification.sms.dlq --partitions 3 --replication-factor 1 --if-not-exists
docker exec kafka kafka-topics --create --bootstrap-server localhost:9092 --topic notification.push.dlq --partitions 3 --replication-factor 1 --if-not-exists
```

### 3) Ejecutar worker

```bash
cd NotificacionWorker
dotnet run
```

## Mensajes de prueba

Enviar a `notification.request`.

`Key` puede ir vacía en pruebas. En producción se recomienda una `key` estable (ej. `orderId` o `eventId`) para mejor particionado/orden.

Ejemplo:

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

## Configuración clave (`appsettings.json`)

```json
{
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "GroupId": "notification-worker-group",
    "Topics": {
      "NotificationRequest": "notification.request",
      "NotificationRequestDlq": "notification.request.dlq",
      "NotificationEmail": "notification.email",
      "NotificationEmailDlq": "notification.email.dlq",
      "NotificationSms": "notification.sms",
      "NotificationSmsDlq": "notification.sms.dlq",
      "NotificationPush": "notification.push",
      "NotificationPushDlq": "notification.push.dlq"
    },
    "RetryPolicy": {
      "MaxProcessingAttempts": 3,
      "MaxDlqPublishAttempts": 3,
      "BackoffMilliseconds": 500
    }
  }
}
```

## Semántica de reintentos (actual)

Por cada mensaje:

1. Reintenta procesamiento hasta `MaxProcessingAttempts`
2. Si no logra procesar, intenta DLQ hasta `MaxDlqPublishAttempts`
3. Si DLQ también falla, no hay commit y el mensaje se reintenta en ciclos posteriores

## Notas

- Es un proyecto de prueba con envíos simulados
- No implementa todavía idempotencia persistente de consumidor (`eventId` + store)
- Para entorno real de ecommerce, agregar: idempotencia real, observabilidad y políticas operativas de poison-messages
