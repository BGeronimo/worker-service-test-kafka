using Confluent.Kafka;
using Microsoft.Extensions.Options;
using NotificacionWorker.Channels;
using NotificacionWorker.Configuration;
using NotificacionWorker.Services;
using NotificacionWorker.Workers;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<KafkaSettings>(builder.Configuration.GetSection("Kafka"));
builder.Services.Configure<ChannelRoutingSettings>(builder.Configuration.GetSection("ChannelRouting"));
builder.Services.Configure<EmailTemplateSettings>(builder.Configuration.GetSection("EmailTemplates"));

builder.Services.AddSingleton<IProducer<string, string>>(sp =>
{
    var kafkaSettings = sp.GetRequiredService<IOptions<KafkaSettings>>().Value;
    var producerConfig = new ProducerConfig
    {
        BootstrapServers = kafkaSettings.BootstrapServers
    };
    return new ProducerBuilder<string, string>(producerConfig).Build();
});

builder.Services.AddTransient<IChannelStrategy, EmailChannelStrategy>();
builder.Services.AddTransient<IChannelStrategy, SmsChannelStrategy>();
builder.Services.AddTransient<IChannelStrategy, PushChannelStrategy>();

builder.Services.AddSingleton<IChannelStrategyFactory, ChannelStrategyFactory>();
builder.Services.AddSingleton<IEmailTemplateRenderer, FileEmailTemplateRenderer>();

builder.Services.AddSingleton<INotificationOrchestrator, NotificationOrchestrator>();

builder.Services.AddHostedService<MainRouterWorker>();
builder.Services.AddHostedService<EmailWorker>();
builder.Services.AddHostedService<SmsWorker>();
builder.Services.AddHostedService<PushWorker>();

var host = builder.Build();
host.Run();
