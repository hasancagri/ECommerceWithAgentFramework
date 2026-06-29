using AgentOrchestrator;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using OpenAI;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// IHttpContextAccessor: per-request kullanici bearer'ini downstream MCP cagrilarina tasimak icin
// (UserTokenForwardingHandler). Registration olmadan framework HttpContext'i AsyncLocal'a yazmaz.
builder.Services.AddHttpContextAccessor();

// OpenAI ayarları konfigürasyondan (user-secrets / ortam değişkeni). Azure'a gerek yok.
string apiKey = builder.Configuration["OpenAI:ApiKey"]
                ?? throw new InvalidOperationException(
                    "OpenAI:ApiKey is not set. Run: dotnet user-secrets set \"OpenAI:ApiKey\" \"sk-...\"");
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

// ── Authentication ───────────────────────────────────────────────────────────
// Hem catalog hem basket: Authorization STATIK degil. Gelen istegin HttpContext'inden
// forward edilir (UserTokenForwardingHandler) => giris yapmis kullanicida onun bearer'i gider.
// HttpContext yoksa (startup tool kesfi / anonim) fallback servis token'ina dusulur.
const string fallbackToken = "dummy-fallback-token";

HttpClient ForwardingHttpClient() =>
    new(new UserTokenForwardingHandler(new HttpContextAccessor(), fallbackToken)
    {
        InnerHandler = new HttpClientHandler(),
    });

var catalogClient = await McpClient.CreateAsync(new HttpClientTransport(
    new HttpClientTransportOptions { Name = "catalog", Endpoint = new Uri(catalogUrl) },
    ForwardingHttpClient(),
    NullLoggerFactory.Instance,
    ownsHttpClient: true));

var cartClient = await McpClient.CreateAsync(new HttpClientTransport(
    new HttpClientTransportOptions { Name = "cart", Endpoint = new Uri(cartUrl) },
    ForwardingHttpClient(),
    NullLoggerFactory.Instance,
    ownsHttpClient: true));

var catalogTools = await catalogClient.ListToolsAsync();
var cartTools = await cartClient.ListToolsAsync();


// PUBLIC agent: giriş yapmamış (anonim) kullanıcılar için. Yalnızca katalog/arama araçları.
// Sepet gibi kullanıcıya özel işlemler bu agent'ta YOK; istenirse login'e yönlendirir.
var publicAgent = builder.AddAIAgent(
    "public",
    instructions: """
                  Sen bir alışveriş asistanısın ve giriş yapmamış (anonim) bir kullanıcıyla konuşuyorsun.
                  Kullanıcı ürün ararsa search_products aracını kullan ve sonuçları döndür.
                  Sepete ekleme, sipariş gibi kullanıcıya özel işlemler için YETKİN YOK.
                  Kullanıcı böyle bir şey isterse araç çağırmaya çalışma; kibarca önce giriş yapması
                  gerektiğini söyle.
                  Bir ürün bulunamazsa durumu kullanıcıya açıkça söyle.
                  """);

foreach (var tool in catalogTools)
    publicAgent.WithAITool(tool);

// ASSISTANT agent: giriş yapmış kullanıcılar için. Katalog + sepet araçları.
var assistant = builder.AddAIAgent(
    "assistant",
    instructions: """
                  Sen bir alışveriş asistanısın ve giriş yapmış bir kullanıcıyla konuşuyorsun.
                  Kullanıcı ürün ararsa search_products aracını kullan.
                  Kullanıcı bir ürünü sepete eklemek niyetini belirtirse (örn. "sepete ekle",
                  "varsa ekle", "ekler misin", "atar mısın") onay için tekrar SORMA; doğrudan
                  add_to_cart aracını bulunan ürünün id'siyle çağır.
                  Kullanıcı sadece arama yaptıysa (ekleme niyeti yoksa) sepete EKLEME, yalnızca sonucu döndür.
                  Bir ürün bulunamazsa sepete ekleme yapma ve durumu kullanıcıya açıkça söyle.
                  """);

foreach (var tool in catalogTools.Concat(cartTools))
    assistant.WithAITool(tool);

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


// Gelen kullanici istegindeki bearer'i downstream MCP (basket) cagrilarina forward eder.
// IHttpContextAccessor AsyncLocal oldugu icin, tool cagrisi istegin SYNC akisinda calistiginda
// o anki kullanicinin token'i akar. HttpContext yoksa (startup tool kesfi / anonim) fallback'e duser.