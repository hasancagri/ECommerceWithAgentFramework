using ChatAgent;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;
using OpenAI;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddHttpContextAccessor();

string apiKey = builder.Configuration["OpenAI:ApiKey"]
                ?? throw new InvalidOperationException(
                    "OpenAI:ApiKey is not set");
string model = builder.Configuration["OpenAI:Model"] ?? "gpt-4o-mini";

IChatClient chatClient = new OpenAIClient(apiKey)
    .GetChatClient(model)
    .AsIChatClient()
    .AsBuilder()
    .ConfigureOptions(o => o.ModelId = model)
    .Build();

builder.Services.AddSingleton(chatClient);

// OpenAI-uyumlu protokolleri etkinleştir.
builder.AddOpenAIChatCompletions();
builder.AddOpenAIResponses();
builder.AddOpenAIConversations();

var gatewayUrl = builder.Configuration["services:gateway:http:0"] ?? "http://localhost:5178";
var basketUrl = $"{gatewayUrl}/mcp/{McpServers.Basket}";
var catalogUrl = $"{gatewayUrl}/mcp/{McpServers.Catalog}";
var customerUrl = $"{gatewayUrl}/mcp/{McpServers.Customer}";
var orderUrl = $"{gatewayUrl}/mcp/{McpServers.Order}";
var paymentUrl = $"{gatewayUrl}/mcp/{McpServers.Payment}";
var stockUrl = $"{gatewayUrl}/mcp/{McpServers.Stock}";
var storefrontUrl = $"{gatewayUrl}/mcp/{McpServers.Storefront}";

// Her agent'in toplayacagi MCP tool'lari: (server, url, baglanacagi named-client, izin verilen tool'lar).
// Tek kaynak; delete_product hicbir listede yok. ClientName = MCP'ye ozel handler/baglanti; kendi
// server'larimiz Identity token forward eder. Yeni bir dis MCP kendi ClientName'iyle eklenir.
// public: yalnizca vitrin aramasi (019 FR-018: Catalog search_products anonim agent'tan cikti).
(string Name, string Url, string ClientName, string[] allowedTools)[] publicAgentTools =
[
    (McpServers.Storefront, storefrontUrl, McpClients.WithToken, [StorefrontTools.SearchStorefrontProducts])
];
// assistant: vitrin aramasi + catalog okuma (sepet akisi icin) + basket + servis-basi okuma tool'lari.
(string Name, string Url, string ClientName, string[] allowedTools)[] assistantAgentTools =
[
    (McpServers.Storefront, storefrontUrl, McpClients.WithToken, [StorefrontTools.SearchStorefrontProducts]),
    (McpServers.Catalog, catalogUrl, McpClients.WithToken, [CatalogTools.SearchProducts, CatalogTools.GetProduct]),
    (McpServers.Basket, basketUrl, McpClients.WithToken,
        [BasketTools.AddToCart, BasketTools.GetBasket, BasketTools.RemoveBasketItem]),
    (McpServers.Order, orderUrl, McpClients.WithToken, [OrderTools.GetOrders]),
    (McpServers.Payment, paymentUrl, McpClients.WithToken, [PaymentTools.GetMyPayments]),
    (McpServers.Stock, stockUrl, McpClients.WithToken, [StockTools.GetStock]),
    // 024: taksit sorgusu icin default kart BIN okumasi (PAN/CVV/token asla).
    (McpServers.Customer, customerUrl, McpClients.WithToken, [CustomerTools.GetDefaultCardBin])
];

builder.Services.AddTransient<TokenInjectingHandler>();

// Iki auth davranisi, iki named-client (yapi tek; MCP hangisini istedigini ClientName ile secer):
// WithToken -> TokenInjectingHandler kullanici token'ini forward eder (kendi server'larimiz).
// NoToken   -> handler yok; token gitmez (dis MCP'ler, or. gmail'i dogrudan cagirirken).
// MCP uzun-omurlu bir SSE GET actigi icin standart resilience handler'i baglantiyi kesip kesfi
// cokertir; bu yuzden ikisini de resilience'tan muaf tutuyoruz (MCP kendi baglanti dongusunu yonetir).
#pragma warning disable EXTEXP0001 // RemoveAllResilienceHandlers experimental; MCP icin gerekli
builder.Services.AddHttpClient(McpClients.WithToken)
    .RemoveAllResilienceHandlers()
    .AddHttpMessageHandler<TokenInjectingHandler>();

builder.Services.AddHttpClient(McpClients.NoToken)
    .RemoveAllResilienceHandlers();

// 024: A2A istemci HttpClient'i. MCP gibi uzun-omurlu SSE tuttugu icin standart resilience/
// timeout akisi keser -> muaf tut + comert timeout. Auth handler YOK (merchant key ertelendi, FR-008).
builder.Services.AddHttpClient(A2APayment.HttpClient, c => c.Timeout = TimeSpan.FromSeconds(60))
    .RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001

builder.Services.AddSingleton<IMcpToolProvider, McpToolProvider>();

// PUBLIC agent (anonim): yalnizca storefront aramasi.
var publicAgent = builder.AddAIAgent("public", (sp, name) =>
{
    var tools = sp.GetRequiredService<IMcpToolProvider>()
        .CollectTools(publicAgentTools);
    return new ChatClientAgent(sp.GetRequiredService<IChatClient>(), Prompts.PublicInstructions, name, null, tools);
}, ServiceLifetime.Singleton);

// ASSISTANT agent (login): catalog + basket.
var assistant = builder.AddAIAgent("assistant", (sp, name) =>
{
    var tools = sp.GetRequiredService<IMcpToolProvider>()
        .CollectTools(assistantAgentTools);

    // 024: uzak A2A PaymentAgent taksit tool'u. Url yok/erisilemezse null -> eklenmez
    // (graceful-degrade, US2). Boot'ta bir kez kurulur (Singleton factory), MCP CollectTools gibi bloklar.
    var a2aTool = A2AInstallmentTool.TryBuildAsync(
        sp.GetRequiredService<IConfiguration>(),
        sp.GetRequiredService<IHttpClientFactory>(),
        sp.GetRequiredService<ILoggerFactory>().CreateLogger("A2AInstallment")).GetAwaiter().GetResult();
    if (a2aTool is not null)
        tools.Add(a2aTool);

    return new ChatClientAgent(sp.GetRequiredService<IChatClient>(), Prompts.AssistantInstructions, name, null, tools);
}, ServiceLifetime.Singleton);

var app = builder.Build();

app.MapDefaultEndpoints();

// Anonim kullanıcı agent'ı: POST /public/v1/chat/completions, /public/v1/responses
app.MapOpenAIChatCompletions(publicAgent);
app.MapOpenAIResponses(publicAgent, "/public/v1/responses");

// Giriş yapmış kullanıcı agent'ı: POST /assistant/v1/chat/completions, /assistant/v1/responses
app.MapOpenAIChatCompletions(assistant);
app.MapOpenAIResponses(assistant, "/assistant/v1/responses");

app.MapOpenAIConversations(); // POST /v1/conversations

await app.RunAsync();