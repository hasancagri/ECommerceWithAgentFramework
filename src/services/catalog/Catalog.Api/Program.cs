using Catalog.Api.Domains.Products;
using Common.Utils.Authorization;
using Common.Utils.Constants;
using Shared.Utils.Constants;

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
    opts.Policies.AddMiddleware(
        typeof(ScopeAuthorizationMiddleware),
        chain => chain.MessageType.GetCustomAttribute<RequiredScopeAttribute>() is not null);
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
});

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

builder.Services.AddHttpContextAccessor();
builder.Services
    .AddMcpServer()
    // Stateful (default): her MCP session onu OLUSTURAN kimlige baglanir. ChatAgent artik her
    // kullanici cagrisi icin TAZE bir user-bound session acar (PerUserMcpTool); boot'taki anonim
    // kesif session'i yalnizca ListTools yapar, hic CallTool yapmaz. Hicbir session iki kimlik
    // arasinda paylasilmadigi icin "user mismatch" (403) cikmaz. Yetki yine handler'daki
    // [RequiredScope] ile kontrol edilir.
    // Tasarim: docs/superpowers/specs/2026-07-08-per-user-mcp-session-design.md
    .WithHttpTransport()
    .WithToolsFromAssembly();


var app = builder.Build();
app.MapScalarDocumentation();

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