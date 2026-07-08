
using Shared.Utils.Constants;

var builder = WebApplication.CreateBuilder(args);
builder.AddOpenApiDocumentation();

var basketDb = builder.Configuration.GetConnectionString("basketDb")!;
builder.Services.AddMarten(opts =>
    {
        opts.DatabaseSchemaName = SchemaConstants.BasketSchemaName;
        opts.Connection(basketDb);
        opts.UseNewtonsoftForSerialization(
            nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
            configure: s => s.ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor);
        opts.Schema.For<Basket.Api.Domains.Baskets.Basket>().Index(x => x.UserId);
    })
    .IntegrateWithWolverine()
    .ApplyAllDatabaseChangesOnStartup();

builder.Host.UseWolverine(opts =>
{
    var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
        .AutoProvision();

    rabbit.DeclareExchange(RabbitMqConstants.OrderCreated.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
        e.BindQueue(RabbitMqConstants.OrderCreated.Queues.Basket);
    });

    opts.ListenToRabbitQueue(RabbitMqConstants.OrderCreated.Queues.Basket);

    opts.Policies.UseDurableLocalQueues();
    // Handler-level yetki: middleware SADECE [RequiredScope] tasiyan komut/sorgulara weave edilir
    // (filter codegen sirasinda bir kez calisir; attribute'suz handler'larda hic cagri yok).
    // REST + MCP ortak nokta.
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
    AuthorizationScopes.BasketRead,
    AuthorizationScopes.BasketWrite);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

// MCP server: [McpServerToolType] isaretli tool'lari (BasketMcpTools) tarar ve HTTP transport ile sunar.
// Tool'lar kullaniciyi (CurrentUser) HttpContext'ten aldigi icin accessor gerekiyor.
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

app.AddBasketGroupEndpointExtension(apiVersionSet);

// Transport kapisi YOK: tool kesfi (ListTools) acilista token'siz calissin. Yetki, komut/sorgu
// handler'larinda ScopeAuthorizationMiddleware ([RequiredScope]) ile. Tool icinde userId forward
// edilen token'dan (CurrentUser) okunur; UseAuthentication global oldugu icin User dolar.
app.MapMcp("/mcp");

await app.RunAsync();