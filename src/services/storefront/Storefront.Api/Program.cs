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

        // 067: pgvector extension'ı şemaya ekler + Npgsql vector type handler kaydeder. Embedding JSONB
        // içinde float[] yaşar; kNN sorgusu (data->>'DescriptionEmbedding')::vector cast'iyle koşar.
        // VectorOn/HNSW bilinçli YOK (research R7): 20k satırda exact scan ms mertebesi, index'e gerek yok.
        opts.UsePgVector();

        // Rich aggregate degil (invariant tasimaz); ProductId, Marten Id'si. Tek composite satir.
        // Optimistic concurrency: farkli kaynaklarin ayni satira eszamanli yazmasinda lost-update
        // olmaz — cakisan handler ConcurrencyException alir, Wolverine retry'da taze yukleyip uygular.
        opts.Schema.For<StorefrontView>().Identity(x => x.ProductId).UseOptimisticConcurrency(true);

        // 054: kullanıcı satın-alma birikimi (kişisel feed sinyali). PK = "{userId:N}:{productId:N}"
        // (idempotent upsert); feed sorgusunun tek erişim yolu UserId — index onun için.
        opts.Schema.For<Storefront.Api.Domains.UserPurchase.UserPurchase>().Index(x => x.UserId);

        // 067: anlamsal temsil AYRI dokümanda (view satırı şişmez; tam-satır okuma yolları etkilenmez).
        // Optimistic concurrency bilinçli YOK: handler/backfill yarışında son yazan kazanır (aynı metnin
        // temsili — içerik eşdeğer). Görünürlük StorefrontView satılabilirlik filtresinde (FR-007).
        opts.Schema.For<Storefront.Api.Domains.StorefrontView.ProductDescriptionEmbedding>()
            .Identity(x => x.ProductId);

        // 069: sorgu izi (ret dahil her query_storefront çağrısı bir satır; FR-006/SC-005).
        opts.Schema.For<Storefront.Api.AgentSql.AgentQueryLog>();
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

    // 054: OrderCompleted → UserPurchase birikimi (kişisel feed sinyali). Binding'i TUKETICI kurar;
    // ayni tek-kuyruk deseni (4. exchange → storefront.events).
    rabbit.DeclareExchange(RabbitMqConstants.OrderCompleted.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
        e.BindQueue(RabbitMqConstants.OrderCompleted.Queues.Storefront);
    });

    // 079: ProductDiscountChanged → StorefrontView.ApplyDiscount. Binding'i TUKETICI kurar (007);
    // aynı tek-kuyruk deseni (5. exchange → storefront.events, Sequential).
    rabbit.DeclareExchange(RabbitMqConstants.ProductDiscountChanged.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
        e.BindQueue(RabbitMqConstants.ProductDiscountChanged.Queues.Storefront);
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
    // Konvansiyonel keşif bu sınıfları atlıyor (nedeni araştırılacak); açık kayıt garantili yol.
    opts.Discovery.IncludeType(typeof(Storefront.Api.CatalogConsumers));
    opts.Discovery.IncludeType(typeof(Storefront.Api.ReviewsConsumers));
    opts.Discovery.IncludeType(typeof(Storefront.Api.StockConsumers));
    opts.Discovery.IncludeType(typeof(Storefront.Api.OrderConsumers));
    opts.Discovery.IncludeType(typeof(Storefront.Api.DiscountConsumers));
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

// 067: OpenAI embedding config — fail-fast (ApiKey yoksa açılmaz; ChatAgent emsali). Tüketici düz T enjekte eder.
builder.Services.AddOptions<OpenAiOption>().BindConfiguration("OpenAI")
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<OpenAiOption>>().Value);
// 069: SemanticSearchOption söküldü (eşik prompt kalıbında); backfill batch ayarı dar option'da.
builder.Services.AddOptions<EmbeddingBackfillOption>().BindConfiguration(nameof(EmbeddingBackfillOption))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<EmbeddingBackfillOption>>().Value);

// 067: embedding üretici — düz deterministik API çağrısı ("agent" davranışı değil; ayrı worker yok).
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
{
    var openAi = sp.GetRequiredService<OpenAiOption>();
    return new OpenAI.OpenAIClient(openAi.ApiKey)
        .GetEmbeddingClient(openAi.EmbeddingModel)
        .AsIEmbeddingGenerator();
});

// 067: geçmiş katalog backfill'i — her açılışta idempotent tarama (FR-008); iş yoksa no-op.
builder.Services.AddHostedService<EmbeddingBackfillService>();

// 069: serbest-sorgu kapısı ayarları (RolePassword user-secrets'tan; fail-fast).
builder.Services.AddOptions<AgentQueryOption>().BindConfiguration(nameof(AgentQueryOption))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<AgentQueryOption>>().Value);

// 069 R1: view + kısıtlı rol bootstrap'ı — AddMarten SONRASI kayıt şart (mt_doc tabloları önce kurulur).
builder.Services.AddHostedService(sp => new Storefront.Api.AgentSql.AgentQuerySurfaceBootstrap(
    storefrontDb,
    sp.GetRequiredService<AgentQueryOption>(),
    sp.GetRequiredService<ILogger<Storefront.Api.AgentSql.AgentQuerySurfaceBootstrap>>()));

// 069 R2: kısıtlı bağlantı — storefrontDb conn-string'i rol kimliğiyle; TEK yetki view SELECT'i.
builder.Services.AddSingleton(sp =>
{
    var opt = sp.GetRequiredService<AgentQueryOption>();
    var csb = new Npgsql.NpgsqlConnectionStringBuilder(storefrontDb)
    {
        Username = opt.RoleName,
        Password = opt.RolePassword
    };
    return new Storefront.Api.AgentSql.AgentQueryConnectionSource(
        Npgsql.NpgsqlDataSource.Create(csb.ConnectionString));
});

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

app.UseAuthentication();
app.UseApiKeyAuthentication();
app.UseAuthorization();

app.MapMcp("/mcp");

await app.RunAsync();