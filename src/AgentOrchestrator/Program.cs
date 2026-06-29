using AgentOrchestrator;
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


// IChatClient'ı DI'a kaydet (doğrudan OpenAI). FixedModelChatClient: istekteki "model"
// alanini (proxy agent adini gonderiyor) yok sayip configdeki modeli zorlar.
IChatClient chatClient = new FixedModelChatClient(
    new OpenAIClient(apiKey)
        .GetChatClient(model)
        .AsIChatClient(),
    model);

builder.Services.AddSingleton(chatClient);

// OpenAI-uyumlu protokolleri etkinleştir.
builder.AddOpenAIChatCompletions();
builder.AddOpenAIResponses();
builder.AddOpenAIConversations();

var gatewayUrl = builder.Configuration["services:gateway:http:0"] ?? "http://localhost:5178";
var basketUrl = $"{gatewayUrl}/mcp/{McpServers.Basket}";
var catalogUrl = $"{gatewayUrl}/mcp/{McpServers.Catalog}";

builder.Services.AddHttpClient<IMcpToolProvider, RequestScopedMcpToolProvider>();

// NOT: Agent'lar Singleton. MapOpenAI* helper'lari agent'i ACILISTA root provider'dan tek
// sefer cozup closure'a yakaliyor (Scoped calismaz). Sonuc: tool'lar acilista BIR KEZ, kullanici
// token'i olmadan toplanir => per-user MCP tool akisi simdilik calismaz (ertelenmis auth borcu).
// Dogru cozum: IHttpContextAccessor ile her istekte tool kuran request-aware agent.

// PUBLIC agent (anonim): yalnizca catalog.
var publicAgent = builder.AddAIAgent("public", (sp, name) =>
{
    var tools = sp.GetRequiredService<IMcpToolProvider>()
        .CollectTools((McpServers.Catalog, catalogUrl));
    return new ChatClientAgent(sp.GetRequiredService<IChatClient>(), Prompts.PublicInstructions, name, null, tools);
}, ServiceLifetime.Singleton);

// ASSISTANT agent (login): catalog + basket.
var assistant = builder.AddAIAgent("assistant", (sp, name) =>
{
    var tools = sp.GetRequiredService<IMcpToolProvider>()
        .CollectTools((McpServers.Catalog, catalogUrl), (McpServers.Basket, basketUrl));
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

app.Run();