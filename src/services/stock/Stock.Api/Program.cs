
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddOpenApiDocumentation();

var stockDb = builder.Configuration.GetConnectionString("stockDb")!;
builder.Services.AddMarten(opts =>
    {
        opts.DatabaseSchemaName = SchemaConstants.StockSchemaName;
        opts.Connection(stockDb);
        opts.UseNewtonsoftForSerialization(
            nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
            configure: s => s.ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor);
        // 012: son-urun yarisi optimistic concurrency ile cozulur (cift satis yok / SC-001).
        opts.Schema.For<ProductStock>().Index(x => x.ProductId).UseOptimisticConcurrency(true);

        // barkod ↔ ProductId eşlemesi (Catalog ProductAdded yazar).
        opts.Schema.For<BarcodeLink>();
    })
    .IntegrateWithWolverine()
    .ApplyAllDatabaseChangesOnStartup();

builder.Host.UseWolverine(opts =>
{
    // Dev: tek dugum (Solo) - leader election/node-agent koordinasyonu kapali; kirli kapanan
    // debug oturumlarinin hayalet-node StopRemoteAgent timeout gurultusunu kokten onler.
    if (builder.Environment.IsDevelopment())
        opts.Durability.Mode = DurabilityMode.Solo;

    var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
        .AutoProvision();

    rabbit.DeclareExchange(RabbitMqConstants.StockChanged.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
        e.BindQueue(RabbitMqConstants.StockChanged.Queues.Storefront);
    });

    opts.PublishMessage<Shared.IntegrationEvents.StockChangedEvent>()
        .ToRabbitExchange(RabbitMqConstants.StockChanged.Exchange);

    // 050/051: Catalog ProductAdded tüketicisi — barkod↔ProductId eşlemesi + ilk OnHand (binding'i tüketici kurar).
    // Sıralı kuyruk (aynı barkod sıralı işlenir). İlk yayıncı = kitap import (051); feed söküldü (050).
    rabbit.DeclareExchange(RabbitMqConstants.ProductAdded.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
        e.BindQueue(RabbitMqConstants.ProductAdded.Queues.Stock);
    });
    opts.ListenToRabbitQueue(RabbitMqConstants.ProductAdded.Queues.Stock).Sequential();

    // 049: checkout stok komutlarını (Commit/RevertCommit) dinle; yanıtları orchestrator reply kuyruğuna.
    opts.ListenToRabbitQueue(RabbitMqConstants.Checkout.StockCommandsQueue);
    opts.PublishMessage<CheckoutMessages.StockCommitted>().ToRabbitQueue(RabbitMqConstants.Checkout.RepliesQueue);
    opts.PublishMessage<CheckoutMessages.StockCommitReverted>().ToRabbitQueue(RabbitMqConstants.Checkout.RepliesQueue);

    opts.Policies.UseDurableLocalQueues();
    // Handler-level yetki: middleware SADECE [RequiredScope] tasiyan komut/sorgulara weave edilir.
    // REST + MCP ortak yetki noktasi.
    opts.Policies.AddMiddleware(
        typeof(Common.Utils.Authorization.ScopeAuthorizationMiddleware),
        chain => chain.MessageType.GetCustomAttribute<Common.Utils.Authorization.RequiredScopeAttribute>() is not null);
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
    // Konvansiyonel keşif event-handler sınıfını atlayabiliyor (Storefront emsali) — açık kayıt garantili yol.
    opts.Discovery.IncludeType(typeof(Stock.Api.StockEventHandlers));
});

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
string[] stockAdminToolNames =
    [Shared.StockAdminTools.SetStock, Shared.StockAdminTools.AdjustStock, Shared.StockAdminTools.ListAllStock];
builder.Services
    .AddMcpServer()
    .WithHttpTransport(http => http.ConfigureSessionOptions = (ctx, opts, _) =>
    {
        var isAdmin = ctx.Request.Path.StartsWithSegments("/mcp-admin");
        var tools = opts.ToolCollection;
        if (tools is null)
            return Task.CompletedTask;
        foreach (var tool in tools
                     .Where(t => stockAdminToolNames.Contains(t.ProtocolTool.Name) != isAdmin).ToArray())
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
app.MapScalarDocumentation();

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