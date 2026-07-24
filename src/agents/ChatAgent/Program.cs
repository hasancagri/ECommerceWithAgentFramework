using ChatAgent;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;
using OpenAI;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddHttpContextAccessor();

// 009: kalıcı konuşma deposu — chatAgentDb, Marten (repo standardı Newtonsoft ayarları).
var chatAgentDb = builder.Configuration.GetConnectionString("chatAgentDb")!;
builder.Services.AddMarten(opts =>
    {
        opts.DatabaseSchemaName = SchemaConstants.ChatAgentSchemaName;
        opts.Connection(chatAgentDb);
        opts.UseNewtonsoftForSerialization(
            nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
            configure: s =>
            {
                s.ConstructorHandling = Newtonsoft.Json.ConstructorHandling.AllowNonPublicDefaultConstructor;
            });

        // DocumentAlias şart: alias verilmezse tablo adı ÇALIŞMA ANI locale'iyle küçültülüyor —
        // tr'de "ItemDocument" → "ıtemdocument" (noktasız ı) çıktı; locale değişirse tablo "kaybolur".
        opts.Schema.For<ChatAgent.Conversations.ConversationDocument>()
            .DocumentAlias("conversation");
        opts.Schema.For<ChatAgent.Conversations.ConversationItemDocument>()
            .DocumentAlias("conversation_item")
            .Index(x => x.ConversationId);
    })
    .ApplyAllDatabaseChangesOnStartup();

// R2-pivot: MAF'ın Conversations depolama arayüzleri INTERNAL (1.15.0-alpha dahil) — takas imkânsız.
// Kalıcılık public seam'den: kendi ConversationStore'umuz + /v1/chat ucu (session'a geçmiş enjekte).
builder.Services.AddSingleton<ChatAgent.Conversations.ConversationStore>();

// Anonim sahipsiz konuşmalar 24s aktivitesizlikte silinir (FR-009); login'e TTL yok (FR-008).
builder.Services.AddHostedService<ChatAgent.Conversations.AnonymousConversationCleanup>();

// 009/R4: my-conversations uçları için JWT doğrulama. Audience DOĞRULANMAZ: forward edilen kullanıcı
// token'ının audience'ları servis API'leridir; ChatAgent'a ayrı audience/scope töreni açılmadı.
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["IdentityOption:Address"] ?? "https://localhost:5001";
        options.RequireHttpsMetadata = false;
        options.MapInboundClaims = false; // sub/email kısa adlarıyla kalsın
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateAudience = false,
            ValidateIssuer = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
        };
    });
builder.Services.AddAuthorization();

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

// OpenAI-uyumlu protokolleri etkinleştir. AddOpenAIConversations bilinçli YOK (R2 — in-memory kaydederdi).
builder.AddOpenAIChatCompletions();
builder.AddOpenAIResponses();

var gatewayUrl = builder.Configuration["services:gateway:http:0"] ?? "http://localhost:5178";
var basketUrl = $"{gatewayUrl}/mcp/{McpServers.Basket}";
var catalogUrl = $"{gatewayUrl}/mcp/{McpServers.Catalog}";
var discountUrl = $"{gatewayUrl}/mcp/{McpServers.Discount}";
var orderUrl = $"{gatewayUrl}/mcp/{McpServers.Order}";
var paymentUrl = $"{gatewayUrl}/mcp/{McpServers.Payment}";
var stockUrl = $"{gatewayUrl}/mcp/{McpServers.Stock}";

// Her agent'in toplayacagi MCP tool'lari: (server, url, baglanacagi named-client, izin verilen tool'lar).
// Tek kaynak; delete_product hicbir listede yok. ClientName = MCP'ye ozel handler/baglanti; kendi
// server'larimiz Identity token forward eder. Yeni bir dis MCP kendi ClientName'iyle eklenir.
// public: yalnizca arama (add_to_cart olmadigi icin get_product'a gerek yok).
(string Name, string Url, string ClientName, string[] allowedTools)[] publicAgentTools =
[
    (McpServers.Catalog, catalogUrl, McpClients.WithToken, [CatalogTools.SearchProducts])
];
// assistant: catalog okuma + tum basket tool'lari + servis-basi okuma tool'lari (stok, siparis, odeme, indirim).
(string Name, string Url, string ClientName, string[] allowedTools)[] assistantAgentTools =
[
    (McpServers.Catalog, catalogUrl, McpClients.WithToken, [CatalogTools.SearchProducts, CatalogTools.GetProduct]),
    (McpServers.Basket, basketUrl, McpClients.WithToken,
        [BasketTools.AddToCart, BasketTools.GetBasket, BasketTools.RemoveBasketItem,
            BasketTools.ApplyDiscountCoupon, BasketTools.RemoveDiscountCoupon]),
    (McpServers.Discount, discountUrl, McpClients.WithToken, [DiscountTools.GetDiscount]),
    (McpServers.Order, orderUrl, McpClients.WithToken, [OrderTools.GetOrders]),
    (McpServers.Payment, paymentUrl, McpClients.WithToken, [PaymentTools.GetMyPayments]),
    (McpServers.Stock, stockUrl, McpClients.WithToken, [StockTools.GetStock])
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
#pragma warning restore EXTEXP0001

builder.Services.AddSingleton<IMcpToolProvider, McpToolProvider>();

// PUBLIC agent (anonim): yalnizca catalog.
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
    return new ChatClientAgent(sp.GetRequiredService<IChatClient>(), Prompts.AssistantInstructions, name, null, tools);
}, ServiceLifetime.Singleton);

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapMyConversationsEndpoints();
app.MapChatStreamEndpoint(); // widget'ın kalıcı-geçmişli sohbet yolu (R1-pivot)

// Anonim kullanıcı agent'ı: POST /public/v1/chat/completions, /public/v1/responses
app.MapOpenAIChatCompletions(publicAgent);
app.MapOpenAIResponses(publicAgent, "/public/v1/responses");

// Giriş yapmış kullanıcı agent'ı: POST /assistant/v1/chat/completions, /assistant/v1/responses
app.MapOpenAIChatCompletions(assistant);
app.MapOpenAIResponses(assistant, "/assistant/v1/responses");

// Framework'ün /v1/conversations ucu bilinçli map'lenmez (R5): yaratma sahiplikli kendi ucumuzdan.

await app.RunAsync();