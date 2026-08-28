
var builder = WebApplication.CreateBuilder(args);
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

    // 012 (US4): TTL dolunca sweep job'i yayinlar; Basket tuketip sepet satirini siler.
    rabbit.DeclareExchange(RabbitMqConstants.ReservationExpired.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
    });

    opts.PublishMessage<Shared.IntegrationEvents.ReservationExpired>()
        .ToRabbitExchange(RabbitMqConstants.ReservationExpired.Exchange);

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

// 012: rezervasyon TTL/sweep config binding.
builder.Services.AddOptions<ReservationOptions>()
    .BindConfiguration(ReservationOptions.SectionName)
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<ReservationOptions>(
    sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ReservationOptions>>().Value);

builder.Services.AddAuthenticationAndAuthorizationExtension(
    builder.Configuration,
    AuthorizationScopes.StockWrite,
    AuthorizationScopes.StockReserve);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

// L2 (paylaşımlı) önbellek katmanı — Redis IDistributedCache; opsiyonel (yoksa HybridCache yalnız L1).
if (builder.Configuration.GetConnectionString("redis") is not null)
    builder.AddRedisDistributedCache("redis");

// Declarative caching aspect'i: HybridCache + IMessageBus'ı şeffaf sar. UseWolverine'den sonra olmalı.
builder.Services.AddCachingAspect("stock");

// 012: Stock rezervasyon gRPC sunucusu (Basket/Order senkron cagirir).
builder.Services.AddGrpc();

// 012 (US4): TTL sweep icin Hangfire (008 deseni; Postgres 'hangfire' semasi, Marten'a dokunmaz).
builder.Services.AddHangfire(cfg => cfg
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(pg => pg.UseNpgsqlConnection(stockDb),
        new PostgreSqlStorageOptions { SchemaName = "hangfire" }));
builder.Services.AddHangfireServer();

builder.Services.AddHttpContextAccessor();
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

// Dis tuketiciler icin opak UserKey (X-User-Key) custom auth semasi.
builder.Services.AddApiKeyAuthentication(builder.Configuration);

var app = builder.Build();
app.MapScalarDocumentation();

var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

app.UseAuthentication();
app.UseApiKeyAuthentication();
app.UseAuthorization();

app.AddStockGroupEndpointExtension(apiVersionSet);

app.MapMcp("/mcp");

// 012: gRPC rezervasyon servisi; yetki endpoint seviyesinde (userId cagri govdesinde).
app.MapGrpcService<StockReservationGrpcService>()
    .RequireAuthorization(AuthorizationScopes.StockReserve);

// 012 (US4) + 026: birincil temizlik durable sure-sonu tetigidir (SweepReservation). Bu cron
// yalniz GUVENLIK-AGI: DLQ'ya dusen/kacan tetiklerin biraktigi bayatlari seyrek (~10dk) toplar.
// Ayni idempotent PurgeExpired'i kullanir; durable tetikle cakismaz.
var sweepCron = builder.Configuration.GetValue("Reservations:SweepCron", "*/10 * * * *")!;
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<IRecurringJobManager>()
        .AddOrUpdate<ReservationSweepJob>("reservation-sweep", job => job.RunAsync(CancellationToken.None), sweepCron);
}

await app.RunAsync();