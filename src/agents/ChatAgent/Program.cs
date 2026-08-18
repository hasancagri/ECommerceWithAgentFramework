
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

// 032: DropShop onboarding gateway + onboarding config (strongly-typed; IdentityServerSettings deseni).
// Bölüm yoksa admin persona graceful-degrade eder (tool eklenmez, boot çökmez).
var dropShop = builder.Configuration.GetSection("DropShopGateway").Get<DropShopGatewayOption>();
var onboarding = builder.Configuration.GetSection("Onboarding").Get<OnboardingOption>() ?? new OnboardingOption();
if (dropShop is not null)
    builder.Services.AddSingleton(dropShop);
builder.Services.AddSingleton(onboarding);

// 024: A2A PaymentAgent config — section "PaymentGateway" (house-style Options; config[...] yerine).
builder.Services.AddOptions<PaymentGateway>().BindConfiguration(nameof(PaymentGateway))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<PaymentGateway>(sp => sp.GetRequiredService<IOptions<PaymentGateway>>().Value);

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
    (McpServers.Order, orderUrl, McpClients.WithToken, [OrderTools.GetOrders, OrderTools.PlaceOrder]),
    (McpServers.Payment, paymentUrl, McpClients.WithToken, [PaymentTools.GetMyPayments]),
    (McpServers.Stock, stockUrl, McpClients.WithToken, [StockTools.GetStock]),
    // 024: default kart BIN okumasi (PAN/CVV asla). 038: odeme baglami (kart vault token +
    // gercek buyer — A2A istegine verbatim) + kart listesi (kart secimi). 033 taksit/cekim
    // tool'lari SOKULDU — taksit sorgusu ve cekim A2A -> PaymentGateway zinciriyle.
    (McpServers.Customer, customerUrl, McpClients.WithToken,
        [CustomerTools.GetDefaultCardBin, CustomerTools.GetPaymentContext, CustomerTools.ListCards])
];
// 032: admin persona YALNIZ DropShop onboarding MCP'sini toplar (submit_registration + registration_status).
// Config yoksa bos -> CollectTools bos doner, persona tool'suz acilir (graceful-degrade).
(string Name, string Url, string ClientName, string[] allowedTools)[] adminAgentTools = dropShop is null
    ?
    []
    :
    [
        (McpServers.MerchantOnboarding, dropShop.McpUrl, McpClients.MachineOnboarding,
            [OnboardingTools.SubmitRegistration, OnboardingTools.RegistrationStatus])
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

// 032: DropShop onboarding MCP'ye makine token'i forward eden named-client (MCP uzun-omurlu SSE ->
// resilience muaf). Handler her istege client_credentials Bearer takar (kesif ListTools dahil).
if (dropShop is not null)
{
    builder.Services.AddTransient<OnboardingGatewayTokenHandler>();
    builder.Services.AddHttpClient(McpClients.MachineOnboarding)
        .RemoveAllResilienceHandlers()
        .AddHttpMessageHandler<OnboardingGatewayTokenHandler>();
}
#pragma warning restore EXTEXP0001

builder.Services.AddSingleton<IMcpToolProvider, McpToolProvider>();

// PUBLIC agent (anonim): yalnizca storefront aramasi.
var publicAgent = builder.AddAIAgent("public", (sp, name) =>
{
    var tools = sp.GetRequiredService<IMcpToolProvider>()
        .CollectTools(publicAgentTools);
    return new ChatClientAgent(sp.GetRequiredService<IChatClient>(), Prompts.PublicInstructions, name, null, tools);
}, ServiceLifetime.Singleton);

// ASSISTANT agent (login): catalog + basket + odeme (A2A).
var assistant = builder.AddAIAgent("assistant", (sp, name) =>
{
    // 038: taksit sorgusu + kayitli kartla cekim A2A uzerinden PaymentGateway Payment.Agent'a
    // devredildi (033'un Customer.Api MCP odeme yolu SOKULDU — tek yol A2A). Odeme baglami
    // (vault token + buyer) Customer.Api get_payment_context'ten gelir, A2A istegine verbatim.
    var tools = sp.GetRequiredService<IMcpToolProvider>()
        .CollectTools(assistantAgentTools);

    var a2aLogger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("ChatAgent.A2APayment");
    var paymentAgentTool = ChatAgent.ExternalAgents.PaymentAgentInstallmentTool.TryBuildAsync(
            sp.GetRequiredService<PaymentGateway>(),
            sp.GetRequiredService<IHttpClientFactory>(),
            a2aLogger)
        .GetAwaiter().GetResult();
    if (paymentAgentTool is not null)
        tools.Add(paymentAgentTool);

    return new ChatClientAgent(sp.GetRequiredService<IChatClient>(), Prompts.AssistantInstructions, name, null, tools);
}, ServiceLifetime.Singleton);

// 032: ADMIN agent (admin rolu, WebApp BFF admin kolu). YALNIZ onboarding tool'lari; shopper araclari YOK.
// 016 push-inline: basvuru alanlari prompt'a boot'ta gomulur (config'ten). Gateway config yok -> tool'suz
// acilir (graceful-degrade); prompt "kullanilamiyor" der. Singleton (framework agent'lari boot'ta yakalar).
var adminAgent = builder.AddAIAgent("admin", (sp, name) =>
{
    var tools = sp.GetRequiredService<IMcpToolProvider>().CollectTools(adminAgentTools);

    // 029: alan seti gateway'in 023 Merchant sözleşmesi (tip + tip-uyum matrisi alanları).
    var instructions = dropShop is null
        ? Prompts.AdminOnboardingInstructions
        : $"{Prompts.AdminOnboardingInstructions}\n\n" +
          "submit_registration çağrısında bu mağazanın başvuru alanlarını kullan:\n" +
          $"- type: {onboarding.Type}\n" +
          $"- name: {onboarding.Name}\n" +
          $"- email: {onboarding.Email}\n" +
          $"- gsmNumber: {onboarding.GsmNumber}\n" +
          $"- address: {onboarding.Address}\n" +
          $"- iban: {onboarding.Iban}\n" +
          $"- contactName: {onboarding.ContactName}\n" +
          $"- contactSurname: {onboarding.ContactSurname}\n" +
          $"- identityNumber: {onboarding.IdentityNumber}\n" +
          $"- taxOffice: {onboarding.TaxOffice}\n" +
          $"- taxNumber: {onboarding.TaxNumber}\n" +
          $"- legalCompanyTitle: {onboarding.LegalCompanyTitle}\n" +
          "Boş görünen opsiyonel alanları çağrıya HİÇ gönderme (uydurma değer üretme).\n" +
          $"registration_status sorgusunu bu mağazanın e-postasıyla ({onboarding.Email}) yap.";

    return new ChatClientAgent(sp.GetRequiredService<IChatClient>(), instructions, name, null, tools);
}, ServiceLifetime.Singleton);

var app = builder.Build();

app.MapDefaultEndpoints();

// Anonim kullanıcı agent'ı: POST /public/v1/chat/completions, /public/v1/responses
app.MapOpenAIChatCompletions(publicAgent);
app.MapOpenAIResponses(publicAgent, "/public/v1/responses");

// Giriş yapmış kullanıcı agent'ı: POST /assistant/v1/chat/completions, /assistant/v1/responses
app.MapOpenAIChatCompletions(assistant);
app.MapOpenAIResponses(assistant, "/assistant/v1/responses");

// 032: admin onboarding agent'ı: POST /admin/v1/responses (WebApp admin BFF kolu buraya proxy'ler).
app.MapOpenAIResponses(adminAgent, "/admin/v1/responses");

app.MapOpenAIConversations(); // POST /v1/conversations

await app.RunAsync();