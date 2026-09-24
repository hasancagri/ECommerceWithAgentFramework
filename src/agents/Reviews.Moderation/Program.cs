using Reviews.Moderation.Options;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Moderasyon model config'i ZORUNLU — acilista fail-fast (section "OpenAI"; user-secrets bu projede).
builder.Services.AddOptions<ModerationOptions>()
    .BindConfiguration(ModerationOptions.SectionName)
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<ModerationOptions>(sp =>
    sp.GetRequiredService<IOptions<ModerationOptions>>().Value);

// Agent Singleton'dir (konvansiyon: framework baslangicta yakalar).
builder.Services.AddSingleton<ModerationAgent>();

// Mesajlaşma (Wolverine + RabbitMQ topoloji + handler keşfi) → Extensions/MessagingExtensions.cs.
builder.AddModerationMessaging();

var app = builder.Build();
app.MapDefaultEndpoints();
await app.RunAsync();
