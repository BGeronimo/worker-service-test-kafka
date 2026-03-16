namespace NotificacionWorker.Configuration;

public class KafkaSettings
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string GroupId { get; set; } = "notification-worker-group";
    public Topics Topics { get; set; } = new();
    public RetryPolicySettings RetryPolicy { get; set; } = new();
}

public class RetryPolicySettings
{
    public int MaxProcessingAttempts { get; set; } = 3;
    public int MaxDlqPublishAttempts { get; set; } = 3;
    public int BackoffMilliseconds { get; set; } = 500;
}

public class Topics
{
    public string NotificationRequest { get; set; } = "notification.request";
    public string NotificationRequestDlq { get; set; } = "notification.request.dlq";
    public string NotificationEmail { get; set; } = "notification.email";
    public string NotificationEmailDlq { get; set; } = "notification.email.dlq";
    public string NotificationSms { get; set; } = "notification.sms";
    public string NotificationSmsDlq { get; set; } = "notification.sms.dlq";
    public string NotificationPush { get; set; } = "notification.push";
    public string NotificationPushDlq { get; set; } = "notification.push.dlq";
}
