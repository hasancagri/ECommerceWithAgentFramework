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


// IChatClient'ı DI'a kaydet (doğrudan OpenAI). Istekteki "model" alani proxy agent adini
// (public/assistant) tasiyor ve per-request ModelId olarak default'u eziyordu; ConfigureOptions
// ile her cagride configdeki gercek modeli geri zorla (yoksa OpenAI "model_not_found" verir).
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

// Her agent'in toplayacagi MCP tool'lari: (server, url, o server'dan izin verilen tool'lar).
// Tek kaynak; delete_product hicbir listede yok.
// public: yalnizca arama (add_to_cart olmadigi icin get_product'a gerek yok).
(string Name, string Url, string[] AllowedTools)[] publicAgentTools =
[
    (McpServers.Catalog, catalogUrl, [CatalogTools.SearchProducts])
];
// assistant: catalog okuma + tum basket tool'lari.
(string Name, string Url, string[] AllowedTools)[] assistantAgentTools =
[
    (McpServers.Catalog, catalogUrl, [CatalogTools.SearchProducts, CatalogTools.GetProduct]),
    (McpServers.Basket, basketUrl,
        [BasketTools.AddToCart, BasketTools.GetBasket, BasketTools.RemoveBasketItem,
            BasketTools.ApplyDiscountCoupon, BasketTools.RemoveDiscountCoupon])
];

builder.Services.AddTransient<TokenInjectingHandler>();
#pragma warning disable EXTEXP0001 // RemoveAllResilienceHandlers experimental; MCP icin gerekli
builder.Services.AddHttpClient<IMcpToolProvider, McpToolProvider>()
    // MCP, sunucu->istemci icin uzun-omurlu bir SSE GET acar; ServiceDefaults'in standart
    // resilience handler'inin TotalRequestTimeout'u bu baglantiyi iptal edip kesfi cokertiyor.
    // Bu yuzden MCP client'ini resilience'tan muaf tutuyoruz (MCP kendi baglanti yasam dongusunu yonetir).
    .RemoveAllResilienceHandlers()
    .AddHttpMessageHandler<TokenInjectingHandler>();
#pragma warning restore EXTEXP0001

// NOT: Agent'lar Singleton (zorunlu): MapOpenAI* helper'lari agent'i ACILISTA root provider'dan
// tek sefer cozup closure'a yakaliyor (Scoped kayit boot'ta crash eder). Tool KESFI de acilista
// bir kez, anonim ListTools ile yapilir (allowlist statik). Toplanan tool'lar PerUserMcpTool'dur:
// her cagride, HttpClient'a takili TokenInjectingHandler'in forward ettigi kullanici token'iyla TAZE
// bir user-bound stateful session acar, cagriyi yapar, session'i kapatir. Hicbir session iki kimlik
// arasinda paylasilmadigi icin stateful sunucular "user mismatch" (403) uretmez; yetki yine Wolverine
// handler middleware'inde ([RequiredScope]) kontrol edilir. => per-user MCP tool akisi CALISIR.
// Tasarim: docs/superpowers/specs/2026-07-08-per-user-mcp-session-design.md

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

app.MapDefaultEndpoints();

// Anonim kullanıcı agent'ı: POST /public/v1/chat/completions, /public/v1/responses
app.MapOpenAIChatCompletions(publicAgent);
app.MapOpenAIResponses(publicAgent, "/public/v1/responses");

// Giriş yapmış kullanıcı agent'ı: POST /assistant/v1/chat/completions, /assistant/v1/responses
app.MapOpenAIChatCompletions(assistant);
app.MapOpenAIResponses(assistant, "/assistant/v1/responses");

app.MapOpenAIConversations(); // POST /v1/conversations

await app.RunAsync();