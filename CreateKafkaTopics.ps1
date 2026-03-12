# Script para crear los topics de Kafka antes de iniciar los workers
# Esto evita errores de "topic not available" al inicio

Write-Host "╔════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║   CREACIÓN DE TOPICS DE KAFKA             ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

$topics = @(
    "notification.request",
    "notification.email",
    "notification.sms",
    "notification.push"
)

Write-Host "⏳ Verificando que Kafka esté disponible..." -ForegroundColor Yellow
$kafkaRunning = docker ps --filter "name=kafka" --filter "status=running" --format "{{.Names}}"

if (-not $kafkaRunning) {
    Write-Host "❌ Kafka no está corriendo. Ejecuta 'docker-compose up -d' primero" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Kafka está corriendo" -ForegroundColor Green
Write-Host ""

foreach ($topic in $topics) {
    Write-Host "📝 Creando topic: $topic" -ForegroundColor Yellow

    $result = docker exec kafka kafka-topics --create `
        --bootstrap-server localhost:9092 `
        --topic $topic `
        --partitions 3 `
        --replication-factor 1 `
        --if-not-exists 2>&1

    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✅ Topic '$topic' creado/verificado" -ForegroundColor Green
    } else {
        Write-Host "  ⚠️  Topic '$topic': $result" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "📊 Listando todos los topics:" -ForegroundColor Cyan
docker exec kafka kafka-topics --list --bootstrap-server localhost:9092

Write-Host ""
Write-Host "✨ ¡Listo! Ahora puedes ejecutar tu Worker Service" -ForegroundColor Green
Write-Host "   Comando: cd NotificacionWorker; dotnet run" -ForegroundColor White
