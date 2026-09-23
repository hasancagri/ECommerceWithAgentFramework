var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
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
        
        // Gtin (barkod) ürün lookup/teklik anahtarıdır — lookup index'i.
        // 045: FamilyCode agent okumaları için ucuz lookup index'i (gruplama Storefront'ta).
        opts.Schema.For<Product>().Index(x => x.Gtin).Index(x => x.FamilyCode);

        // 040 K9: ProductTag yeni aggregate — dış yüzeyi yok, şemada yaşar (besleyen akış 041+).
        opts.Schema.For<ProductTag>();

        // 058: fiyat geçmişi append-only kaydı — ürün bazlı okuma için lookup index'i.
        opts.Schema.For<ProductPriceChange>().Index(x => x.ProductId);

        // 016: NormalizedName teklik anahtarıdır (R4) — computed unique index son güvence.
        // Legacy Brand migrasyonu YOK (kullanıcı kararı): DB sıfırlanarak başlatılır, katalog feed'den dolar.
        opts.Schema.For<Category>().UniqueIndex(Marten.Schema.UniqueIndexType.Computed, x => x.NormalizedName);
        // 052: Brand→Author rename + yeni Publisher — ikisi de NormalizedName teklik anahtarı (get-or-create güvencesi).
        opts.Schema.For<Catalog.Api.Domains.Authors.Author>()
            .UniqueIndex(Marten.Schema.UniqueIndexType.Computed, x => x.NormalizedName);
        opts.Schema.For<Catalog.Api.Domains.Publishers.Publisher>()
            .UniqueIndex(Marten.Schema.UniqueIndexType.Computed, x => x.NormalizedName);

        // 043: özellik registry'si — NormalizedName teklik anahtarı (seed get-or-create güvencesi).
        opts.Schema.For<Catalog.Api.Domains.SpecificationAttributes.SpecificationAttribute>()
            .UniqueIndex(Marten.Schema.UniqueIndexType.Computed, x => x.NormalizedName);

        // 083: Excel import staging + capability token. Token teklik anahtarı (link=yetki); ImportRow
        // ISBN idempotency + Status processor sorgusu (WHERE Status=Pending) için lookup index'i.
        // DocumentAlias ZORUNLU: tip adı büyük I ile başlıyor, tr-TR makinede varsayılan alias
        // lowercase'i noktasız ı üretir → computed-index delta her boot bozulur (42P07). ascii alias baypas.
        opts.Schema.For<Catalog.Api.Import.ImportSession>()
            .DocumentAlias("importsession")
            .UniqueIndex(Marten.Schema.UniqueIndexType.Computed, x => x.Token);
        opts.Schema.For<Catalog.Api.Import.ImportRow>()
            .DocumentAlias("importrow")
            .Index(x => x.Isbn).Index(x => x.Status);
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

    rabbit.DeclareExchange(RabbitMqConstants.ProductChanged.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
        e.BindQueue(RabbitMqConstants.ProductChanged.Queues.Storefront);
    });

    opts.PublishMessage<Shared.IntegrationEvents.ProductChangedEvent>()
        .ToRabbitExchange(RabbitMqConstants.ProductChanged.Exchange);

    // 050/051: yayınlanan üründe barkod↔ProductId eşlemesi Stock'a duyurulur (yayıncı yalnız exchange deklare eder).
    // İlk yayıncı = kitap import (051); feed 050'de söküldü.
    rabbit.DeclareExchange(RabbitMqConstants.ProductAdded.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
    });
    opts.PublishMessage<Shared.IntegrationEvents.ProductAdded>()
        .ToRabbitExchange(RabbitMqConstants.ProductAdded.Exchange);

    // 083 T022: File.Api'nin CoverIngested'ini tüket (Catalog'un İLK consumer'ı). Binding'i tüketici
    // kurar (007 dersi); FileConsumers.Handle → Product.SetImage → ProductChangedEvent.
    rabbit.DeclareExchange(RabbitMqConstants.CoverIngested.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
        e.BindQueue(RabbitMqConstants.CoverIngested.Queues.Catalog);
    });
    opts.ListenToRabbitQueue(RabbitMqConstants.CoverIngested.Queues.Catalog);

    opts.Policies.UseDurableLocalQueues();
    opts.Policies.AddMiddleware(
        typeof(ScopeAuthorizationMiddleware),
        chain => chain.MessageType.GetCustomAttribute<RequiredScopeAttribute>() is not null);
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
    // Wolverine keşfi çoğul *Consumers sınıfını taramaz → açıkça ekle (ZORUNLU; yoksa mesaj yutulur).
    opts.Discovery.IncludeType(typeof(Catalog.Api.FileConsumers));
});

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ReportApiVersions = true;
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

// Admin yüzeyi (/mcp-admin) ikiye ayrılır: okuma AdminCatalogRead, yazma AdminCatalogWrite.
string[] catalogAdminScopes =
[
    AuthorizationScopes.AdminCatalogRead,
    AuthorizationScopes.AdminCatalogWrite,
];

builder.Services.AddAuthenticationAndAuthorizationExtension(
    builder.Configuration,
    catalogAdminScopes);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

// 083 D6/T010: Excel yükleme ekranı config'i (link tabanı + ömür) — section "ImportOptions".
// Tüketici düz T enjekte eder (078 emsali; IOptions<T> değil).
builder.Services.AddOptions<Catalog.Api.Options.ImportOptions>()
    .BindConfiguration(nameof(Catalog.Api.Options.ImportOptions))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<Catalog.Api.Options.ImportOptions>(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Catalog.Api.Options.ImportOptions>>().Value);

// 083 T013/FR-002+FR-004: bekleyen staging satırlarını arka planda ürüne çeviren dayanıklı süreç
// (Process/ deseni). 051 books.json seeder'ı söküldü (FR-011) — Excel import tek katalog giriş yolu.
builder.Services.AddHostedService<Catalog.Api.Import.ImportProcessor>();

// L2 (paylaşımlı) önbellek katmanı — Redis IDistributedCache; opsiyonel (yoksa HybridCache yalnız L1).
if (builder.Configuration.GetConnectionString("redis") is not null)
    builder.AddRedisDistributedCache("redis");

// Declarative caching aspect'i: HybridCache + IMessageBus'ı şeffaf sar. UseWolverine'den sonra olmalı.
builder.Services.AddCachingAspect("catalog");

builder.Services.AddHttpContextAccessor();
// 070: TEK MCP server, İKİ uç — anonim /mcp (keşif) + korumalı /mcp-admin (yönetim). Oturum
// başına TAZE options (SDK, ConfigureSessionOptions verilince IOptionsFactory'den yeni kurar);
// tool seti isteğin yoluna göre budanır: admin tool'lar YALNIZ /mcp-admin'de görünür.
string[] catalogAdminToolNames =
[
    Shared.CatalogAdminTools.ListProducts, Shared.CatalogAdminTools.GetProduct,
    Shared.CatalogAdminTools.UpdateProduct, Shared.CatalogAdminTools.SetPublished,
    Shared.CatalogAdminTools.GetPriceHistory,
    // 074: parite tool'ları (REST admin söküldü) — hepsi YALNIZ /mcp-admin'de.
    Shared.CatalogAdminTools.CreateProduct, Shared.CatalogAdminTools.SetProductDimensions,
    Shared.CatalogAdminTools.SetProductSeo, Shared.CatalogAdminTools.AssignProductTag,
    Shared.CatalogAdminTools.RemoveProductTag, Shared.CatalogAdminTools.CreateCategory,
    Shared.CatalogAdminTools.UpdateCategory, Shared.CatalogAdminTools.CreateAuthor,
    Shared.CatalogAdminTools.CreateProductTag, Shared.CatalogAdminTools.RenameProductTag,
    Shared.CatalogAdminTools.ListProductTags, Shared.CatalogAdminTools.CreateSpecificationAttribute,
    Shared.CatalogAdminTools.AddSpecificationAttributeOption, Shared.CatalogAdminTools.ListSpecificationAttributes,
    Shared.CatalogAdminTools.RepublishProducts,
    // 083: Excel katalog import — hepsi YALNIZ /mcp-admin (allowlist tuzağı — eklemeyen tool'u kaybeder).
    Shared.CatalogAdminTools.ImportCatalog, Shared.CatalogAdminTools.PublishImported,
    Shared.CatalogAdminTools.GetImportStatus,
];
builder.Services
    .AddMcpServer()
    .WithHttpTransport(http => http.ConfigureSessionOptions = (ctx, opts, _) =>
    {
        var isAdmin = ctx.Request.Path.StartsWithSegments("/mcp-admin");
        var tools = opts.ToolCollection;
        if (tools is null)
            return Task.CompletedTask;
        foreach (var tool in tools
                     .Where(t => catalogAdminToolNames.Contains(t.ProtocolTool.Name) != isAdmin).ToArray())
            tools.Remove(tool);
        return Task.CompletedTask;
    })
    .WithToolsFromAssembly();

// 070: /mcp-admin RFC 9728 keşfi (401 challenge + metadata) — admin scope'uyla; anonim /mcp etkilenmez.
builder.Services.AddMcpAdminResourceMetadata(builder.Configuration, "catalog", catalogAdminScopes);


// Dis tuketiciler icin opak UserKey (X-User-Key) custom auth semasi.
builder.Services.AddApiKeyAuthentication(builder.Configuration);

var app = builder.Build();
// AppHost WithHttpHealthCheck("/health") bu ucu yoklar (Development-only map).
app.MapDefaultEndpoints();
app.MapScalarDocumentation();

app.UseAuthentication();
app.UseApiKeyAuthentication();
app.UseAuthorization();

// 074: domain iş REST yüzeyi söküldü — catalog admin/okuma tümüyle MCP (/mcp + /mcp-admin).
// Ürün girişi = Excel import (083) + admin_create_product (MCP). Kalan REST = MCP-infra + import ekranı.

// 083 US1/FR-001: hosted xlsx yükleme ekranı — ANONİM (token = yetki; İLKE V v1.11.1 capability-link
// istisnası). MapMcp'DEN ÖNCE map'lenir; auth token URL'inde taşınır (078 emsali).
app.MapImportUploadEndpoints();

app.MapMcp("/mcp");

// 070: korumalı yönetim ucu — kimliksiz istek 401 + resource_metadata challenge (OAuth zinciri
// buradan başlar); scope katmanı handler'larda, tool-bazlı ([RequiredScope(ProductCreate)] vb.).
app.MapMcp("/mcp-admin").RequireAuthorization();
app.MapMcpResourceMetadata();

await app.RunAsync();