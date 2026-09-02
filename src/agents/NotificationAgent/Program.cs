using NotificationAgent;
using NotificationAgent.Options;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Bildirim model config'i ZORUNLU — acilista fail-fast (section "OpenAI"; user-secrets bu projede).
builder.Services.AddOptions<NotificationOptions>()
    .BindConfiguration(NotificationOptions.SectionName)
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<NotificationOptions>(sp =>
    sp.GetRequiredService<IOptions<NotificationOptions>>().Value);

// Mail linki mutlak WebApp adresiyle kurulur (relatif link Mailpit UI'da 404 — canli bulgu).
builder.Services.AddOptions<WebAppOptions>()
    .BindConfiguration(WebAppOptions.SectionName)
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<WebAppOptions>(sp =>
    sp.GetRequiredService<IOptions<WebAppOptions>>().Value);

// Agent Singleton'dir (konvansiyon: framework baslangicta yakalar).
builder.Services.AddSingleton<MailAgent>();

builder.Host.UseWolverine(opts =>
{
    // Dev: tek dugum (Solo) — repo konvansiyonu (hayalet-node gurultusunu onler).
    if (builder.Environment.IsDevelopment())
        opts.Durability.Mode = DurabilityMode.Solo;

    var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
        .AutoProvision();

    // Tuketici: kendi kuyrugunu deklare edilen exchange'e baglar (007 dersi) + dinler.
    rabbit.DeclareExchange(RabbitMqConstants.PriceAlarmTriggered.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
        e.BindQueue(RabbitMqConstants.PriceAlarmTriggered.Queues.Worker);
    });
    opts.ListenToRabbitQueue(RabbitMqConstants.PriceAlarmTriggered.Queues.Worker);

    // Yayinci: yalniz exchange deklare eder (binding tuketici Library'de).
    rabbit.DeclareExchange(RabbitMqConstants.NotificationSent.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
    });
    opts.PublishMessage<IntegrationEvents.NotificationSent>()
        .ToRabbitExchange(RabbitMqConstants.NotificationSent.Exchange);

    // Gonderim hatasi: retry 10s/30s/60s → error queue (FR-008; Compose LLM hatasi buraya girmez).
    opts.OnException<NotificationException>()
        .RetryWithCooldown(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60))
        .Then.MoveToErrorQueue();

    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
    // *EventHandlers (çoğul) Wolverine isim-konvansiyonunca keşfedilMEZ — elle dahil et
    // (Reviews.Moderation emsali).
    opts.Discovery.IncludeType(typeof(PriceAlarmEventHandlers));
});

var app = builder.Build();
app.MapDefaultEndpoints();
await app.RunAsync();