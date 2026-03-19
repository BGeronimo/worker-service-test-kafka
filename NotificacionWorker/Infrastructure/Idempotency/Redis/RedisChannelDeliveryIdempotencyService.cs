using Microsoft.Extensions.Options;
using NotificacionWorker.Application.Idempotency;
using NotificacionWorker.Configuration;
using StackExchange.Redis;

namespace NotificacionWorker.Infrastructure.Idempotency.Redis;

public class RedisChannelDeliveryIdempotencyService : IChannelDeliveryIdempotencyService
{
    private readonly ILogger<RedisChannelDeliveryIdempotencyService> _logger;
    private readonly IDatabase _database;
    private readonly RedisIdempotencySettings _settings;
    private readonly TimeSpan _sentTtl;
    private readonly TimeSpan _processingLockTtl;

    public RedisChannelDeliveryIdempotencyService(
        ILogger<RedisChannelDeliveryIdempotencyService> logger,
        IConnectionMultiplexer connectionMultiplexer,
        IOptions<RedisIdempotencySettings> options)
    {
        _logger = logger;
        _database = connectionMultiplexer.GetDatabase();
        _settings = options.Value;
        _sentTtl = TimeSpan.FromHours(Math.Clamp(_settings.SentTtlHours, 1, 24 * 30));
        _processingLockTtl = TimeSpan.FromSeconds(Math.Clamp(_settings.ProcessingLockSeconds, 10, 600));
    }

    public async Task<ChannelDeliveryLease> TryAcquireAsync(string eventId, string channelName, CancellationToken cancellationToken = default)
    {
        ValidateKeyInputs(eventId, channelName);

        var sentKey = BuildSentKey(eventId, channelName);
        if (await _database.KeyExistsAsync(sentKey))
        {
            return new ChannelDeliveryLease(ChannelDeliveryAcquireResult.AlreadyProcessed);
        }

        var processingKey = BuildProcessingKey(eventId, channelName);
        var lockToken = Guid.NewGuid().ToString("N");
        var acquired = await _database.StringSetAsync(
            processingKey,
            lockToken,
            _processingLockTtl,
            When.NotExists);

        if (acquired)
        {
            return new ChannelDeliveryLease(ChannelDeliveryAcquireResult.Acquired, lockToken);
        }

        if (await _database.KeyExistsAsync(sentKey))
        {
            return new ChannelDeliveryLease(ChannelDeliveryAcquireResult.AlreadyProcessed);
        }

        _logger.LogWarning("Se detectó entrega en progreso para EventId {EventId} y canal {Channel}", eventId, channelName);
        return new ChannelDeliveryLease(ChannelDeliveryAcquireResult.InProgress);
    }

    public async Task MarkSucceededAsync(string eventId, string channelName, string? lockToken, CancellationToken cancellationToken = default)
    {
        ValidateKeyInputs(eventId, channelName);

        var sentKey = BuildSentKey(eventId, channelName);
        var processingKey = BuildProcessingKey(eventId, channelName);

        await _database.StringSetAsync(
            sentKey,
            DateTimeOffset.UtcNow.ToString("O"),
            _sentTtl,
            When.Always);

        await ReleaseProcessingLockAsync(processingKey, lockToken);
    }

    public async Task MarkFailedAsync(string eventId, string channelName, string? lockToken, CancellationToken cancellationToken = default)
    {
        ValidateKeyInputs(eventId, channelName);

        var processingKey = BuildProcessingKey(eventId, channelName);
        await ReleaseProcessingLockAsync(processingKey, lockToken);
    }

    private async Task ReleaseProcessingLockAsync(string processingKey, string? lockToken)
    {
        if (string.IsNullOrWhiteSpace(lockToken))
        {
            return;
        }

        const string script = "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end";
        await _database.ScriptEvaluateAsync(
            script,
            [(RedisKey)processingKey],
            [(RedisValue)lockToken]);
    }

    private string BuildSentKey(string eventId, string channelName)
    {
        return $"{_settings.KeyPrefix}:sent:{eventId}:{channelName}";
    }

    private string BuildProcessingKey(string eventId, string channelName)
    {
        return $"{_settings.KeyPrefix}:processing:{eventId}:{channelName}";
    }

    private static void ValidateKeyInputs(string eventId, string channelName)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            throw new ArgumentException("EventId es requerido para aplicar idempotencia.", nameof(eventId));
        }

        if (string.IsNullOrWhiteSpace(channelName))
        {
            throw new ArgumentException("ChannelName es requerido para aplicar idempotencia.", nameof(channelName));
        }
    }
}
