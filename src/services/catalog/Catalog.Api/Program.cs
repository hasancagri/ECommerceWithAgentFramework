var builder = WebApplication.CreateBuilder(args);
builder.AddOpenApiDocumentation();

var catalogDb = builder.Configuration.GetConnectionString("catalogDb")!;
builder.Services.AddMarten(opts =>
    {
        opts.DatabaseSchemaName = SchemaConstants.CatalogSchemaName;
        opts.Connection(catalogDb);
        opts.UseNewtonsoftForSerialization(
            nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
            configure: s =>
            {
                s.ConstructorHandling = Newtonsoft.Json.ConstructorHandling.AllowNonPublicDefaultConstructor;
            });
        
        // 041: Gtin (barkod) Procurement upsert anahtarıdır — lookup index'i.
        opts.Schema.For<Product>().Index(x => x.Gtin);

        // 040 K9: ProductTag yeni aggregate — dış yüzeyi yok, şemada yaşar (besleyen akış 041+).
        opts.Schema.For<ProductTag>();

        // 016: NormalizedName teklik anahtarıdır (R4) — computed unique index son güvence.
        // Legacy Brand migrasyonu YOK (kullanıcı kararı): DB sıfırlanarak başlatılır, katalog feed'den dolar.
        opts.Schema.For<Category>().UniqueIndex(Marten.Schema.UniqueIndexType.Computed, x => x.NormalizedName);
        opts.Schema.For<Brand>().UniqueIndex(Marten.Schema.UniqueIndexType.Computed, x => x.NormalizedName);
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

    rabbit.DeclareExchange(RabbitMqConstants.UploadCoursePicture.Exchange,
        e => { e.ExchangeType = ExchangeType.Fanout; });

    rabbit.DeclareExchange(RabbitMqConstants.CoursePictureUploaded.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
        e.BindQueue(RabbitMqConstants.CoursePictureUploaded.Queues.Catalog);
    });
    
    rabbit.DeclareExchange(RabbitMqConstants.ProductChanged.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
        e.BindQueue(RabbitMqConstants.ProductChanged.Queues.Storefront);
    });

    opts.PublishMessage<Shared.IntegrationEvents.ProductChangedEvent>()
        .ToRabbitExchange(RabbitMqConstants.ProductChanged.Exchange);

    // 041: Procurement yayınlarının tüketicisi — TEK sıralı kuyruk (aynı barkod sıralı işlenir).
    // Binding'i TÜKETİCİ kurar (007 dersi); iki exchange de aynı kuyruğa bağlanır.
    rabbit.DeclareExchange(RabbitMqConstants.CanonicalProduct.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
        e.BindQueue(RabbitMqConstants.CanonicalProduct.Queues.Catalog);
    });
    rabbit.DeclareExchange(RabbitMqConstants.BuyBoxChanged.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
        e.BindQueue(RabbitMqConstants.BuyBoxChanged.Queues.Catalog);
    });
    opts.ListenToRabbitQueue(RabbitMqConstants.ProcurementEvents.CatalogQueue).Sequential();

    // 041: yeni üründe barkod↔ProductId eşlemesi Stock'a duyurulur (yayıncı yalnız exchange deklare eder).
    rabbit.DeclareExchange(RabbitMqConstants.ProductLinked.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
    });
    opts.PublishMessage<Shared.IntegrationEvents.ProductLinked>()
        .ToRabbitExchange(RabbitMqConstants.ProductLinked.Exchange);

    opts.ListenToRabbitQueue(RabbitMqConstants.CoursePictureUploaded.Queues.Catalog);

    opts.Policies.UseDurableLocalQueues();
    opts.Policies.AddMiddleware(
        typeof(ScopeAuthorizationMiddleware),
        chain => chain.MessageType.GetCustomAttribute<RequiredScopeAttribute>() is not null);
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
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
    AuthorizationScopes.CatalogWrite);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

// 041: kanonik Category>SubCategory ağacı (Procurement kopyasıyla ad-hizalı; idempotent).
builder.Services.AddHostedService<Catalog.Api.Seeding.CatalogTaxonomySeedHostedService>();

// L2 (paylaşımlı) önbellek katmanı — Redis IDistributedCache; opsiyonel (yoksa HybridCache yalnız L1).
if (builder.Configuration.GetConnectionString("redis") is not null)
    builder.AddRedisDistributedCache("redis");

// Declarative caching aspect'i: HybridCache + IMessageBus'ı şeffaf sar. UseWolverine'den sonra olmalı.
builder.Services.AddCachingAspect("catalog");

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

app.AddProductGroupEndpointExtension(apiVersionSet);
app.AddProductTagGroupEndpointExtension(apiVersionSet);
app.AddCategoryGroupEndpointExtension(apiVersionSet);
app.AddBrandGroupEndpointExtension(apiVersionSet);

app.MapMcp("/mcp");

await app.RunAsync();