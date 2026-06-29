using Catalog.Api.Domains.Products;
using Common.Utils.Authorization;
using Common.Utils.Constants;
using Shared.Utils.Constants;

var builder = WebApplication.CreateBuilder(args);

var catalogDb = builder.Configuration.GetConnectionString("catalogDb")!;
builder.Services.AddMarten(opts =>
    {
        opts.DatabaseSchemaName = SchemaConstants.CATALOG_SCHEMA_NAME;
        opts.Connection(catalogDb);
        opts.UseNewtonsoftForSerialization(
            nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
            configure: s =>
            {
                s.ConstructorHandling = Newtonsoft.Json.ConstructorHandling.AllowNonPublicDefaultConstructor;
            });
        
        opts.Schema.For<Product>();
    })
    .IntegrateWithWolverine()
    .ApplyAllDatabaseChangesOnStartup();


builder.Host.UseWolverine(opts =>
{
    var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
        .AutoProvision();

    rabbit.DeclareExchange(RabbitMqConstants.UploadCoursePicture.Exchange,
        e => { e.ExchangeType = ExchangeType.Fanout; });

    rabbit.DeclareExchange(RabbitMqConstants.CoursePictureUploaded.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
        e.BindQueue(RabbitMqConstants.CoursePictureUploaded.Queues.Catalog);
    });

    // Binding'i publisher da tanimlasin: fanout exchange'e bagli kuyruk publish aninda yoksa
    // mesaj sessizce dusurulur. Kuyrugu Catalog da bildirince, Stock henuz ayaga kalkmamis olsa
    // bile mesajlar kalici kuyrukta birikir, kaybolmaz (startup sirasindan bagimsiz).
    rabbit.DeclareExchange(RabbitMqConstants.ProductCreated.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
        e.BindQueue(RabbitMqConstants.ProductCreated.Queues.Stock);
    });

    opts.PublishMessage<Shared.IntegrationEvents.ProductCreatedEvent>()
        .ToRabbitExchange(RabbitMqConstants.ProductCreated.Exchange);

    opts.ListenToRabbitQueue(RabbitMqConstants.CoursePictureUploaded.Queues.Catalog);

    opts.Policies.UseDurableLocalQueues();
    // Handler-level yetki: [RequiredScope] tasiyan komut/sorgular icin token scope kontrolu
    // (REST + MCP ortak nokta).
    opts.Policies.AddMiddleware(typeof(ScopeAuthorizationMiddleware));
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
});

// Seed'i Wolverine'den SONRA kaydet: hosted service'ler kayit sirasiyla baslar, boylece
// SeedData.StartAsync calistiginda Wolverine runtime hazir olur ve PublishAsync calisir.
builder.Services.AddHostedService<SeedData>();

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ReportApiVersions = true;
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

builder.Services.AddAuthenticationAndAuthorizationExtension(
    builder.Configuration,
    AuthorizationScopes.CatalogRead,
    AuthorizationScopes.CatalogWrite);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

// MCP server: [McpServerToolType] isaretli tool'lari (ProductMcpTools) tarar ve HTTP transport ile sunar.
// Tool icindeki scope kontrolu icin HttpContext'e erisim gerekiyor.
builder.Services.AddHttpContextAccessor();
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();


var app = builder.Build();

var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

app.UseAuthentication();
app.UseAuthorization();

app.AddProductGroupEndpointExtension(apiVersionSet);

// Transport kapisi YOK: tool kesfi (ListTools) acilista token'siz calissin. Yetki, komut/sorgu
// handler'larinda ScopeAuthorizationMiddleware ([RequiredScope]) ile kontrol edilir. UseAuthentication
// global oldugu icin token GELDIGINDE HttpContext.User yine dolar.
app.MapMcp("/mcp");

await app.RunAsync();