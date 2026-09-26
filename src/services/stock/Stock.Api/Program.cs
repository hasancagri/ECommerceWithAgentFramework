
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Kalıcılık (Marten + şema/index + Wolverine entegrasyonu) → Extensions/MartenExtensions.cs.
builder.AddStockMarten();

// Mesajlaşma (Wolverine + RabbitMQ topoloji + handler keşfi) → Extensions/MessagingExtensions.cs.
// SIRA: AddCachingAspect'ten ÖNCE (cache aspect IMessageBus'ı sarar).
builder.AddStockMessaging();

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ReportApiVersions = true;
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

builder.Services.AddAuthenticationAndAuthorizationExtension(
    builder.Configuration,
    AuthorizationScopes.StockWrite);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

// L2 (paylaşımlı) önbellek katmanı — Redis IDistributedCache; opsiyonel (yoksa HybridCache yalnız L1).
if (builder.Configuration.GetConnectionString("redis") is not null)
    builder.AddRedisDistributedCache("redis");

// Declarative caching aspect'i: HybridCache + IMessageBus'ı şeffaf sar. UseWolverine'den sonra olmalı.
builder.Services.AddCachingAspect("stock");

builder.Services.AddHttpContextAccessor();
// 085 R1: TEK uç /mcp (085 — /mcp-admin öldü). Oturum başına TAZE options (SDK, ConfigureSessionOptions
// verilince IOptionsFactory'den yeni kurar); tool seti isteği ATAN TOKEN'IN SCOPE'una göre budanır
// (yol-prefix değil): admin tool'lar YALNIZ ilgili scope token'da varsa görünür.
builder.Services
    .AddMcpServer()
    .WithHttpTransport(http => http.ConfigureSessionOptions = (ctx, opts, _) =>
    {
        var tools = opts.ToolCollection;
        if (tools is null)
            return Task.CompletedTask;
        foreach (var tool in tools
                     .Where(t => !McpScopePruningExtension.IsToolVisible(
                         t.ProtocolTool.Name, Stock.Api.Mcp.StockAdminSurface.ToolScopeMap, ctx.User)).ToArray())
            tools.Remove(tool);
        return Task.CompletedTask;
    })
    .WithToolsFromAssembly();

// Dis tuketiciler icin opak UserKey (X-User-Key) custom auth semasi.
builder.Services.AddApiKeyAuthentication(builder.Configuration);

var app = builder.Build();
// AppHost WithHttpHealthCheck("/health") bu ucu yoklar (Development-only map).
app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseApiKeyAuthentication();
app.UseAuthorization();

// 074: domain iş REST yüzeyi söküldü — stok okuma/yönetim tümüyle MCP (/mcp).
// Checkout saga stok düşümü broker (CommitStock/RevertCommitStock handler'ları) — REST endpoint YOK.

// 085: TEK uç — anonim (get_stock); admin tool'lar scope-budamalı görünür, scope katmanı handler'da
// ([RequiredScope(StockWrite)], 403 son savunma). /mcp-admin öldü.
app.MapMcp("/mcp");

await app.RunAsync();