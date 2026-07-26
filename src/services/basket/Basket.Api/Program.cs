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
    // Dev: tek dugum (Solo) - leader election/node-agent koordinasyonu kapali; kirli kapanan
    // debug oturumlarinin hayalet-node StopRemoteAgent timeout gurultusunu kokten onler.
    if (builder.Environment.IsDevelopment())
        opts.Durability.Mode = DurabilityMode.Solo;

    // 012: gRPC tipli client (AddGrpcClient) opaque bir factory'dir; Wolverine handler codegen'i
    // onu inline kuramaz ve service-location ister. StockReservationClientProxy handler'a enjekte
    // edildiginden bu izne ihtiyac var (uyari ile birak — smell gorunur kalsin).
    opts.ServiceLocationPolicy = JasperFx.CodeGeneration.Model.ServiceLocationPolicy.AllowedButWarn;

    var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
        .AutoProvision();

    rabbit.DeclareExchange(RabbitMqConstants.OrderCreated.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
        e.BindQueue(RabbitMqConstants.OrderCreated.Queues.Basket);
    });

    opts.ListenToRabbitQueue(RabbitMqConstants.OrderCreated.Queues.Basket);

    // 012 (US4): TTL dolunca Stock yayinlar; sepet satirini sileriz.
    rabbit.DeclareExchange(RabbitMqConstants.ReservationExpired.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
        e.BindQueue(RabbitMqConstants.ReservationExpired.Queues.Basket);
    });

    opts.ListenToRabbitQueue(RabbitMqConstants.ReservationExpired.Queues.Basket);

    opts.Policies.UseDurableLocalQueues();
    opts.Policies.AddMiddleware(
        typeof(Common.Utils.Authorization.ScopeAuthorizationMiddleware),
        chain => chain.MessageType.GetCustomAttribute<Common.Utils.Authorization.RequiredScopeAttribute>() is not null);
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
    // *EventHandlers static sinifi ad konvansiyonuyla otomatik kesfedilmiyor (Storefront deseni);
    // acikca dahil et — yoksa OrderCreated (sepet temizligi) ve ReservationExpired (012 US4) calismaz.
    opts.Discovery.IncludeType(typeof(Basket.Api.BasketEventHandlers));
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
// Dış tüketiciler icin opak UserKey (X-User-Key) custom auth semasi. JWT'ye dokunmaz.
builder.Services.AddApiKeyAuthentication(builder.Configuration);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

builder.Services.AddHttpContextAccessor();

// 012: Stock rezervasyon gRPC istemcisi (senkron Reserve/Release). Adres Aspire service discovery;
// kullanici bearer token'i BearerForwardingHandler ile taşınır (stock.reserve scope).
builder.Services.AddTransient<BearerForwardingHandler>();
// gRPC balancer'inin Aspire service-discovery cozumleyicisi YOK (ServiceDiscovery paketi yalniz
// HTTP/YARP resolver'i saglar). Bu yuzden 'stock-api' adini Aspire'in enjekte ettigi cozumlenmis
// endpoint'ten alip somut adresi veriyoruz (balancer 'localhost'u DNS ile cozer).
var stockGrpcAddress = builder.Configuration["services:stock-api:https:0"]
    ?? builder.Configuration["services:stock-api:http:0"]
    ?? "https://stock-api";
builder.Services
    .AddGrpcClient<StockReservation.StockReservationClient>(o => o.Address = new Uri(stockGrpcAddress))
    .AddHttpMessageHandler<BearerForwardingHandler>();
// Proxy'yi somut tipiyle kaydet: handler onu concrete type ile ister (Scrutor yalnizca
// AsImplementedInterfaces kaydediyor → IScopedDependency marker'i somut cozumu vermez).
builder.Services.AddScoped<StockReservationClientProxy>();
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();
app.MapScalarDocumentation();

var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

app.UseAuthentication();
// X-User-Key varsa eager cozer: gecersiz->401, gecerli->principal (UseAuthorization'dan once).
app.UseApiKeyAuthentication();
app.UseAuthorization();

app.AddBasketGroupEndpointExtension(apiVersionSet);

app.MapMcp("/mcp");

await app.RunAsync();