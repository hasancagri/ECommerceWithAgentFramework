var builder = WebApplication.CreateBuilder(args);
builder.AddOpenApiDocumentation();
// Bootstrap (research.md madde 5), Catalog/Stock/Discount'u mantıksal servis adıyla (http://catalog-api
// vb.) çağırıyor — bu isimleri gerçek Aspire endpoint'ine çözen HttpClient service-discovery burada açılır.
builder.AddServiceDefaults();

var storefrontDb = builder.Configuration.GetConnectionString("storefrontDb")!;
builder.Services.AddMarten(opts =>
    {
        opts.DatabaseSchemaName = SchemaConstants.StorefrontSchemaName;
        opts.Connection(storefrontDb);
        opts.UseNewtonsoftForSerialization(
            nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
            configure: s => s.ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor);

        // Hicbiri rich aggregate degil (invariant tasimaz); ProductId, Marten Id'si olarak kullanilir.
        opts.Schema.For<CatalogInfo>().Identity(x => x.ProductId);
        opts.Schema.For<StockInfo>().Identity(x => x.ProductId);
        opts.Schema.For<DiscountInfo>().Identity(x => x.ProductId);
    })
    .IntegrateWithWolverine()
    .ApplyAllDatabaseChangesOnStartup();

builder.Host.UseWolverine(opts =>
{
    var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
        .AutoProvision();

    // Exchange'ler + kuyruk baglamalari kaynak servislerde (Catalog/Stock/Discount) declare edilir;
    // Storefront yalnizca kendi kuyruklarini dinler.
    opts.ListenToRabbitQueue(RabbitMqConstants.ProductChanged.Queues.Storefront);
    opts.ListenToRabbitQueue(RabbitMqConstants.StockChanged.Queues.Storefront);
    opts.ListenToRabbitQueue(RabbitMqConstants.DiscountChanged.Queues.Storefront);

    opts.Policies.UseDurableLocalQueues();
    // Handler-level yetki: middleware SADECE [RequiredScope] tasiyan komut/sorgulara weave edilir.
    // REST + MCP ortak yetki noktasi.
    opts.Policies.AddMiddleware(
        typeof(Common.Utils.Authorization.ScopeAuthorizationMiddleware),
        chain => chain.MessageType.GetCustomAttribute<Common.Utils.Authorization.RequiredScopeAttribute>() is not null);
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
    AuthorizationScopes.StorefrontRead);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

// Bootstrap (research.md madde 5): mevcut m2m.client (catalog.read/discount.read/stock.read) ile
// client_credentials token uretip Catalog/Stock/Discount'un kendi REST uclarini bir kerelik cagirir.
var bootstrapIdentitySettings = builder.Configuration.GetSection("Bootstrap:IdentityServer")
    .Get<Storefront.Api.Bootstrap.BootstrapIdentityServerSettings>()!;
builder.Services.AddSingleton(bootstrapIdentitySettings);
builder.Services.AddHttpClient("identity");
builder.Services.AddHttpClient("catalog-api", c => c.BaseAddress = new Uri("http://catalog-api"));
builder.Services.AddHttpClient("stock-api", c => c.BaseAddress = new Uri("http://stock-api"));
builder.Services.AddHttpClient("discount-api", c => c.BaseAddress = new Uri("http://discount-api"));
builder.Services.AddHostedService<Storefront.Api.Bootstrap.StorefrontBootstrapHostedService>();

builder.Services.AddHttpContextAccessor();
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapScalarDocumentation();

var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

app.UseAuthentication();
app.UseAuthorization();

app.AddStorefrontViewGroupEndpointExtension(apiVersionSet);

app.MapMcp("/mcp");

await app.RunAsync();