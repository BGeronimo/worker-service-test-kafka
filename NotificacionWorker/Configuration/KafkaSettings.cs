namespace NotificacionWorker.Configuration;

public class KafkaSettings
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string GroupId { get; set; } = "notification-worker-group";
    public Topics Topics { get; set; } = new();
}

public class Topics
{
    public string NotificationRequest { get; set; } = "notification.request";
    public string NotificationEmail { get; set; } = "notification.email";
    public string NotificationSms { get; set; } = "notification.sms";
    public string NotificationPush { get; set; } = "notification.push";
}
