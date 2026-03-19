# Sistema de Notificaciones con Kafka

Proyecto de ejemplo con `.NET Worker Service`, `Apache Kafka` y `Redis` para enrutar, componer y procesar notificaciones por canal (`Email`, `SMS`, `Push`) con idempotencia por `eventId + canal`.

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

## Estructura del proyecto (actual)

```text
NotificacionWorker/
├── Program.cs
├── appsettings.json
├── Application/
│   ├── Composition/
│   │   ├── INotificationDataComposer.cs
│   │   ├── INotificationDataComposerResolver.cs
│   │   ├── NotificationDataComposerResolver.cs
│   │   ├── NotificationCompositionDataReader.cs
│   │   └── FallbackNotificationDataComposer.cs (no registrado por diseño)
│   └── Idempotency/
│       ├── IChannelDeliveryIdempotencyService.cs
│       ├── ChannelDeliveryAcquireResult.cs
│       └── ChannelDeliveryLease.cs
├── Features/
│   ├── OrdenCompletada/Composers/
│   ├── PromocionMundialFutbol/Composers/
│   └── AlertaInicioSesion/Composers/
├── Configuration/
│   ├── KafkaSettings.cs
│   ├── ChannelRoutingSettings.cs
│   ├── EmailTemplateSettings.cs
│   ├── PushRoutingSettings.cs
│   └── RedisIdempotencySettings.cs
├── Infrastructure/
│   └── Idempotency/
│       └── Redis/
├── Models/
│   ├── NotificationRequest.cs
│   └── NotificationMessage.cs
├── Services/
│   ├── FileEmailTemplateRenderer.cs
│   ├── PushAppResolver.cs
│   └── NotificationOrchestrator.cs
├── Channels/
│   ├── IChannelStrategy.cs
│   ├── IChannelStrategyFactory.cs
│   ├── ChannelStrategyFactory.cs
│   ├── EmailChannelStrategy.cs
│   ├── SmsChannelStrategy.cs
│   └── PushChannelStrategy.cs
├── Workers/
│   ├── MainRouterWorker.cs
│   ├── EmailWorker.cs
│   ├── SmsWorker.cs
│   └── PushWorker.cs
├── Templates/Email/
├── Credentials/
│   ├── firebase-intelaf.json
│   └── firebase-seguridad.json
└── Infrastructure/Idempotency/Redis/
    └── RedisChannelDeliveryIdempotencyService.cs
```

### ¿Por qué está ordenado así?

La estructura está pensada para separar responsabilidades de negocio y de infraestructura, y hacer el flujo más mantenible cuando crecen eventos/canales:

- `Workers` = capa de transporte Kafka: consumir, reintentar, mover a DLQ y hacer `commit` seguro.
- `Services/NotificationOrchestrator` = coordinación del caso de uso, sin acoplarse a Kafka ni a Redis.
- `Channels` = adaptación por canal (`Email`, `SMS`, `Push`) y publicación final.
- `Application/Composition` + `Features/*/Composers` = reglas de composición por `eventType + channel`.
  - Esto mantiene la lógica de enriquecimiento fuera de `Workers` y fuera de estrategias genéricas.
  - Se mantiene modo estricto: si falta compositor específico, falla con error controlado y trazabilidad.
- `Application/Idempotency` = contratos de deduplicación.
- `Infrastructure/Idempotency/Redis` = implementación concreta (Redis) sin contaminar la lógica de aplicación.

Resultado: se puede agregar un nuevo evento/canal tocando piezas puntuales (compositor + mapeo), sin romper el flujo principal.

## Arquitectura actual (madura por responsabilidades)

Separación principal:

- `Workers`: consumo Kafka, retries, commit y DLQ.
- `Application/Composition`: composición estricta de datos por `eventType + channel`.
- `Channels`: publicación por canal (email/sms/push) con idempotencia aplicada antes de publicar.
- `Application/Idempotency`: contratos de deduplicación.
- `Infrastructure/Idempotency/Redis`: implementación distribuida de dedupe.

Flujo principal:

1. `MainRouterWorker` consume de `notification.request`
2. Asigna/resuelve `eventId` (si no viene, genera hash determinístico del payload)
3. `NotificationOrchestrator` enruta por `eventType` a los canales configurados
4. Cada `ChannelStrategy`:
   - resuelve composición para `eventType + channel`
   - aplica control idempotente en Redis
   - publica en topic de canal
5. Cada worker de canal consume su topic y simula envío

## Composición estricta por evento/canal

La composición está en `Application/Composition` + `Features/*/Composers`.

- Cada compositor implementa `INotificationDataComposer`.
- El resolver busca por `CanHandle(eventType, channelName)`.
- Si no existe compositor para un canal requerido, **falla de forma controlada** (modo estricto), se loguea error y el mensaje entra en retry/DLQ según política.

Esto evita que la lógica de negocio quede dispersa en `Workers` o `Channels`.

## Idempotencia por `eventId + channel` (Redis)

Se implementa mediante `IChannelDeliveryIdempotencyService` y `RedisChannelDeliveryIdempotencyService`.

Llaves de Redis:

- `notification:processing:{eventId}:{channel}` (lock temporal)
- `notification:sent:{eventId}:{channel}` (marca de enviado)

Estados:

- `Acquired`: puede procesar/publicar.
- `AlreadyProcessed`: ya fue enviado antes, se omite.
- `InProgress`: otra instancia lo está procesando, se omite este intento.

Resultado práctico:

- Si un canal falla y otro canal ya envió, los reintentos no duplican el ya enviado.
- Reprocesar desde `notification.request.dlq` no reenvía canales previamente marcados como `sent` (dentro de TTL).

### Resiliencia implementada

- `Producer` idempotente (`EnableIdempotence=true`, `Acks=All`)
- `EnableAutoCommit=false`
- `EnableAutoOffsetStore=false`
- Reintentos configurables para:
  - procesamiento
  - publicación a DLQ
- Idempotencia de canal con store distribuido (`Redis`) para evitar duplicados funcionales
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

### 1) Levantar Kafka + Redis

```bash
docker-compose up -d
```

Kafka UI: `http://localhost:8080`
Redis: `localhost:6379`

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

`eventId` es recomendado/esperado para idempotencia explícita. Si no se envía, el worker genera uno con hash del payload.

Ejemplo:

```json
{
  "eventId": "4b9e5b3f-7f7b-4d8a-9e5f-2f26e2c0f6d1",
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
  "RedisIdempotency": {
    "ConnectionString": "localhost:6379",
    "KeyPrefix": "notification",
    "SentTtlHours": 168,
    "ProcessingLockSeconds": 120
  },
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

## Notas operativas

- Es un proyecto de prueba con envíos simulados
- Ya implementa idempotencia por canal con Redis (`eventId + channel`)
- Para producción: agregar métricas/alertas (dedupe hit, in-progress, publish ok/fail), runbook de replay DLQ y pruebas de concurrencia
