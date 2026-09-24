
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
// 070: TEK MCP server, İKİ uç — anonim /mcp (get_stock) + korumalı /mcp-admin (yönetim). Oturum
// başına TAZE options (SDK, ConfigureSessionOptions verilince IOptionsFactory'den yeni kurar);
// tool seti isteğin yoluna göre budanır: admin tool'lar YALNIZ /mcp-admin'de görünür.
builder.Services
    .AddMcpServer()
    .WithHttpTransport(http => http.ConfigureSessionOptions = (ctx, opts, _) =>
    {
        var isAdmin = ctx.Request.Path.StartsWithSegments("/mcp-admin");
        var tools = opts.ToolCollection;
        if (tools is null)
            return Task.CompletedTask;
        foreach (var tool in tools
                     .Where(t => Stock.Api.Mcp.StockAdminSurface.ToolNames.Contains(t.ProtocolTool.Name) != isAdmin).ToArray())
            tools.Remove(tool);
        return Task.CompletedTask;
    })
    .WithToolsFromAssembly();

// 070: /mcp-admin RFC 9728 keşfi (401 challenge + metadata) — admin scope'uyla; anonim /mcp etkilenmez.
builder.Services.AddMcpAdminResourceMetadata(builder.Configuration, "stock",
    AuthorizationScopes.StockWrite);

// Dis tuketiciler icin opak UserKey (X-User-Key) custom auth semasi.
builder.Services.AddApiKeyAuthentication(builder.Configuration);

var app = builder.Build();
// AppHost WithHttpHealthCheck("/health") bu ucu yoklar (Development-only map).
app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseApiKeyAuthentication();
app.UseAuthorization();

// 074: domain iş REST yüzeyi söküldü — stok okuma/yönetim tümüyle MCP (/mcp get_stock + /mcp-admin).
// Checkout saga stok düşümü broker (CommitStock/RevertCommitStock handler'ları) — REST endpoint YOK.

app.MapMcp("/mcp");

// 070: korumalı yönetim ucu — kimliksiz istek 401 + resource_metadata challenge (OAuth zinciri
// buradan başlar); scope katmanı handler'larda ([RequiredScope(StockWrite)]).
app.MapMcp("/mcp-admin").RequireAuthorization();
app.MapMcpResourceMetadata();

await app.RunAsync();