using OpenAI;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// --- OpenAI client'lari (chat + image) — ayarlar Options Pattern ile ---
var openAiOption = builder.Configuration.GetSection("OpenAI").Get<OpenAIOption>() ?? new OpenAIOption();
if (string.IsNullOrWhiteSpace(openAiOption.ApiKey))
    throw new InvalidOperationException("OpenAI:ApiKey is not set");

var openAi = new OpenAIClient(openAiOption.ApiKey);

IChatClient chatClient = openAi
    .GetChatClient(openAiOption.Model)
    .AsIChatClient()
    .AsBuilder()
    .ConfigureOptions(o => o.ModelId = openAiOption.Model)
    .Build();

builder.Services.AddSingleton(chatClient);
builder.Services.AddSingleton(openAi.GetImageClient(openAiOption.ImageModel));

// --- Agent'lar + executor'lar + workflow (Singleton — framework tipleri) ---
builder.Services.AddSingleton<DescriptionAgent>();
builder.Services.AddSingleton<ImageAgent>();
builder.Services.AddSingleton<DescriptionAgentExecutor>();
builder.Services.AddSingleton<ImageAgentExecutor>();
builder.Services.AddSingleton<EnrichmentWorkflow>();

// --- m2m token + MCP hatti ---
builder.Services.Configure<IdentityOption>(builder.Configuration.GetSection("IdentityOption"));
builder.Services.AddTransient<ClientCredentialsTokenHandler>();

// Identity token client: dev'de self-signed sertifikaya guven (Identity.Server HTTPS zorunlu).
builder.Services.AddHttpClient("identity")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
    });

// MCP HttpClient: her istege m2m token ekler; MCP kendi uzun-omurlu SSE baglantisini yonettiginden
// ServiceDefaults'in resilience handler'indan muaf tutulur (yoksa TotalRequestTimeout kesfi cokertir).
#pragma warning disable EXTEXP0001 // RemoveAllResilienceHandlers experimental; MCP icin gerekli
builder.Services.AddHttpClient(AgentConstants.McpHttpClient)
    .RemoveAllResilienceHandlers()
    .AddHttpMessageHandler<ClientCredentialsTokenHandler>();
#pragma warning restore EXTEXP0001

builder.Services.AddSingleton<EnrichmentMcpClient>();
builder.Services.AddHostedService<EnrichmentBackgroundService>();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapGet("/", () => "Product Enrichment Agent");

await app.RunAsync();