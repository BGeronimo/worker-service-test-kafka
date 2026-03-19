namespace NotificacionWorker.Configuration;

public class RedisIdempotencySettings
{
    public string ConnectionString { get; set; } = "localhost:6379";
    public string KeyPrefix { get; set; } = "notification";
    public int SentTtlHours { get; set; } = 168;
    public int ProcessingLockSeconds { get; set; } = 120;
}
