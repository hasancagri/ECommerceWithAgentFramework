using NotificationAgent;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Bildirim model config'i ZORUNLU — acilista fail-fast (section "OpenAI"; user-secrets bu projede).
builder.Services.AddOptions<NotificationOptions>()
    .BindConfiguration(NotificationOptions.SectionName)
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<NotificationOptions>(sp =>
    sp.GetRequiredService<IOptions<NotificationOptions>>().Value);

// Agent Singleton'dir (konvansiyon: framework baslangicta yakalar).
builder.Services.AddSingleton<MailAgent>();

// Mesajlaşma (Wolverine + RabbitMQ topoloji + handler keşfi) → Extensions/MessagingExtensions.cs.
builder.AddNotificationMessaging();

var app = builder.Build();
app.MapDefaultEndpoints();
await app.RunAsync();