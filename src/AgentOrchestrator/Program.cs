using AgentOrchestrator;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;
using OpenAI;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// IHttpContextAccessor: per-request kullanici bearer'ini downstream MCP cagrilarina tasimak icin
// (RequestScopedMcpToolProvider okur). Registration olmadan framework HttpContext'i AsyncLocal'a yazmaz.
builder.Services.AddHttpContextAccessor();

// OpenAI ayarları konfigürasyondan (user-secrets / ortam değişkeni). Azure'a gerek yok.
string apiKey = builder.Configuration["OpenAI:ApiKey"]
                ?? throw new InvalidOperationException(
                    "OpenAI:ApiKey is not set");
string model = builder.Configuration["OpenAI:Model"] ?? "gpt-4o-mini";

// IChatClient'ı DI'a kaydet (doğrudan OpenAI).
IChatClient chatClient = new OpenAIClient(apiKey)
    .GetChatClient(model)
    .AsIChatClient();

builder.Services.AddSingleton(chatClient);

// OpenAI-uyumlu protokolleri etkinleştir.
builder.AddOpenAIChatCompletions();
builder.AddOpenAIResponses();
builder.AddOpenAIConversations();

// MCP server'lara DOĞRUDAN değil, Gateway (YARP) üzerinden bağlanıyoruz.
// Gateway tek auth/trust sınırı: catalog -> ClientCredential, basket -> Password politikası.
// Gateway adresi Aspire reference'ından gelir; standalone çalıştırmada fallback (localhost:5178).
var gatewayUrl = builder.Configuration["services:gateway:http:0"] ?? "http://localhost:5178";

// Gateway'deki MCP route'ları (bkz. Gateway/appsettings.Development.json):
//   /mcp/catalog -> catalog servisinin MCP endpoint'i
//   /mcp/basket  -> basket servisinin MCP endpoint'i (agent tarafında "cart" diye anılır)
var catalogUrl = $"{gatewayUrl}/mcp/catalog";
var cartUrl = $"{gatewayUrl}/mcp/basket";

// ── MCP araçları: STARTUP'ta değil, İSTEK BAŞINA keşfedilir ─────────────────────
// RequestScopedMcpToolProvider (Scoped) her istekte, o anki HttpContext token'iyla
// MCP client kurar; token'i HttpClient default header'ina basar.
// Paylasimli/startup oturumu YOK; anonim->login kimlik degisimi isteğin token'ina gore cozulur.
builder.Services.AddHttpClient<IMcpToolProvider, RequestScopedMcpToolProvider>();


// PUBLIC agent (anonim): yalnizca katalog araclari. Scoped factory => her istekte,
// o isteğin token'iyla kesfedilen tool'larla yeniden kurulur.
var publicAgent = builder.AddAIAgent("public", (sp, name) =>
{
    var provider = sp.GetRequiredService<IMcpToolProvider>();
    IList<AITool> tools =
    [
        .. provider.GetToolsAsync("catalog", catalogUrl)
            .GetAwaiter()
            .GetResult()
    ];
    return new ChatClientAgent(sp.GetRequiredService<IChatClient>(), Prompts.Instructions, name, null, tools);
}, ServiceLifetime.Scoped);

// ASSISTANT agent (login): katalog + sepet araclari.
var assistant = builder.AddAIAgent("assistant", (sp, name) =>
{
    var provider = sp.GetRequiredService<IMcpToolProvider>();
    var catalog = provider.GetToolsAsync("catalog", catalogUrl)
        .GetAwaiter()
        .GetResult();
    var cart = provider.GetToolsAsync("cart", cartUrl).GetAwaiter().GetResult();
    IList<AITool> tools = [.. catalog, .. cart];
    return new ChatClientAgent(sp.GetRequiredService<IChatClient>(), Prompts.Instructions, name, null, tools);
}, ServiceLifetime.Scoped);

var app = builder.Build();

app.MapDefaultEndpoints();

// Anonim kullanıcı agent'ı: POST /public/v1/chat/completions, /public/v1/responses
app.MapOpenAIChatCompletions(publicAgent);
app.MapOpenAIResponses(publicAgent);

// Giriş yapmış kullanıcı agent'ı: POST /assistant/v1/chat/completions, /assistant/v1/responses
app.MapOpenAIChatCompletions(assistant);
app.MapOpenAIResponses(assistant);

app.MapOpenAIConversations(); // POST /v1/conversations

app.Run();


// Tool kesfi ve cagrisi isteğin akisinda (request scope) yapilir. IHttpContextAccessor
// AsyncLocal oldugu icin o anki kullanicinin token'i RequestScopedMcpToolProvider tarafindan
// okunup MCP HttpClient'in default header'ina basilir. HttpContext yoksa (anonim) header eklenmez.