
using Shared.Utils.Constants;

var builder = WebApplication.CreateBuilder(args);

var basketDb = builder.Configuration.GetConnectionString("basketDb")!;
builder.Services.AddMarten(opts =>
    {
        opts.DatabaseSchemaName = SchemaConstants.BASKET_SCHEMA_NAME;
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
    // Stateless: session'i olusturan kimlige bagli "user mismatch" (403) korumasini kapatir.
    // Agent (Singleton) tool'lari ACILISTA token'siz kesfedip session'i ANONIM acar; login'de
    // ayni session'a kullanici token'i ile gelince SDK 403 doner. Bu tool'lar basit request/
    // response oldugu icin session state'e gerek yok; yetki zaten her istekte kontrol edilir.
    // Stateless = her istek bagimsiz, kimlik bind'i yok.
    .WithHttpTransport(o => o.Stateless = true)
    .WithToolsFromAssembly();

var app = builder.Build();

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