var builder = WebApplication.CreateBuilder(args);
builder.AddOpenApiDocumentation();
builder.AddServiceDefaults();

var storefrontDb = builder.Configuration.GetConnectionString("storefrontDb")!;
builder.Services.AddMarten(opts =>
    {
        opts.DatabaseSchemaName = SchemaConstants.StorefrontSchemaName;
        opts.Connection(storefrontDb);
        opts.UseNewtonsoftForSerialization(
            nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
            configure: s => s.ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor);

        // Rich aggregate degil (invariant tasimaz); ProductId, Marten Id'si. Tek composite satir.
        // Optimistic concurrency: farkli kaynaklarin ayni satira eszamanli yazmasinda lost-update
        // olmaz — cakisan handler ConcurrencyException alir, Wolverine retry'da taze yukleyip uygular.
        opts.Schema.For<StorefrontView>().Identity(x => x.ProductId).UseOptimisticConcurrency(true);
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

    // 044: ReviewSummaryChanged binding'ini TUKETICI kurar (041 dersi); yayinci yalniz exchange
    // deklare eder. Ayni storefront.events kuyruguna baglanir (Sequential — satir yarisi yok).
    rabbit.DeclareExchange(RabbitMqConstants.ReviewSummaryChanged.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
        e.BindQueue(RabbitMqConstants.ReviewSummaryChanged.Queues.Storefront);
    });

    // TEK kuyruk (storefront.events): üç exchange de buraya bağlı; Sequential işleme sayesinde
    // aynı view satırına eşzamanlı yazım olmaz — ConcurrencyException kaynağında çözülür.
    opts.ListenToRabbitQueue(RabbitMqConstants.StorefrontEvents.Queue).Sequential();

    // Composite satirda kaynaklar-arasi eszamanli yazim cakismasi (optimistic concurrency) → retry.
    opts.OnException<JasperFx.ConcurrencyException>().RetryTimes(5);
    opts.Policies.UseDurableLocalQueues();
    // Handler-level yetki: middleware SADECE [RequiredScope] tasiyan komut/sorgulara weave edilir.
    // REST + MCP ortak yetki noktasi.
    opts.Policies.AddMiddleware(
        typeof(Common.Utils.Authorization.ScopeAuthorizationMiddleware),
        chain => chain.MessageType.GetCustomAttribute<Common.Utils.Authorization.RequiredScopeAttribute>() is not null);
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
    // Konvansiyonel keşif bu sınıfı atlıyor (nedeni araştırılacak); açık kayıt garantili yol.
    opts.Discovery.IncludeType(typeof(Storefront.Api.StorefrontEventHandlers));
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
    AuthorizationScopes.StorefrontRead);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

// L2 (paylaşımlı) önbellek katmanı — Redis IDistributedCache; opsiyonel (yoksa HybridCache yalnız L1).
if (builder.Configuration.GetConnectionString("redis") is not null)
    builder.AddRedisDistributedCache("redis");

// Declarative caching aspect'i: HybridCache + IMessageBus'ı şeffaf sar. UseWolverine'den sonra olmalı.
builder.Services.AddCachingAspect("storefront");

builder.Services.AddHttpContextAccessor();
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

// Dis tuketiciler icin opak UserKey (X-User-Key) custom auth semasi.
builder.Services.AddApiKeyAuthentication(builder.Configuration);

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapScalarDocumentation();

var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

app.UseAuthentication();
app.UseApiKeyAuthentication();
app.UseAuthorization();

app.AddStorefrontViewGroupEndpointExtension(apiVersionSet);

app.MapMcp("/mcp");

await app.RunAsync();