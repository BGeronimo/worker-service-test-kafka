using Confluent.Kafka;
using NotificacionWorker.Application.Composition;
using NotificacionWorker.Application.Idempotency;
using Microsoft.Extensions.Options;
using NotificacionWorker.Channels;
using NotificacionWorker.Configuration;
using NotificacionWorker.Features.OrdenCompletada.Composers;
using NotificacionWorker.Features.PromocionMundialFutbol.Composers;
using NotificacionWorker.Infrastructure.Idempotency.Redis;
using NotificacionWorker.Services;
using StackExchange.Redis;
using NotificacionWorker.Workers;
using NotificacionWorker.Features.AlertaInicioSesion.Composers;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options => options.ServiceName = "NotificacionWorker");

builder.Services.Configure<KafkaSettings>(builder.Configuration.GetSection("Kafka"));
builder.Services.Configure<ChannelRoutingSettings>(builder.Configuration.GetSection("ChannelRouting"));
builder.Services.Configure<EmailTemplateSettings>(builder.Configuration.GetSection("EmailTemplates"));
builder.Services.Configure<PushRoutingSettings>(builder.Configuration.GetSection("PushRouting"));
builder.Services.Configure<RedisIdempotencySettings>(builder.Configuration.GetSection("RedisIdempotency"));

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var redisSettings = sp.GetRequiredService<IOptions<RedisIdempotencySettings>>().Value;
    return ConnectionMultiplexer.Connect(redisSettings.ConnectionString);
});

builder.Services.AddSingleton<IProducer<string, string>>(sp =>
{
    var kafkaSettings = sp.GetRequiredService<IOptions<KafkaSettings>>().Value;
    var producerConfig = new ProducerConfig
    {
        BootstrapServers = kafkaSettings.BootstrapServers,
        Acks = Acks.All,
        EnableIdempotence = true
    };
    return new ProducerBuilder<string, string>(producerConfig).Build();
});

builder.Services.AddTransient<IChannelStrategy, EmailChannelStrategy>();
builder.Services.AddTransient<IChannelStrategy, SmsChannelStrategy>();
builder.Services.AddTransient<IChannelStrategy, PushChannelStrategy>();

builder.Services.AddSingleton<IChannelStrategyFactory, ChannelStrategyFactory>();
builder.Services.AddSingleton<INotificationDataComposerResolver, NotificationDataComposerResolver>();
builder.Services.AddSingleton<INotificationDataComposer, OrdenCompletadaEmailComposer>();
builder.Services.AddSingleton<INotificationDataComposer, OrdenCompletadaPushComposer>();
builder.Services.AddSingleton<INotificationDataComposer, PromocionMundialFutbolEmailComposer>();
builder.Services.AddSingleton<INotificationDataComposer, PromocionMundialFutbolSmsComposer>();
builder.Services.AddSingleton<INotificationDataComposer, AlertaInicioSesionPushComposer>();
builder.Services.AddSingleton<IChannelDeliveryIdempotencyService, RedisChannelDeliveryIdempotencyService>();
builder.Services.AddSingleton<IEmailTemplateRenderer, FileEmailTemplateRenderer>();
builder.Services.AddSingleton<IPushAppResolver, PushAppResolver>();

builder.Services.AddSingleton<INotificationOrchestrator, NotificationOrchestrator>();

builder.Services.AddHostedService<MainRouterWorker>();
builder.Services.AddHostedService<EmailWorker>();
builder.Services.AddHostedService<SmsWorker>();
builder.Services.AddHostedService<PushWorker>();

var host = builder.Build();
host.Run();
