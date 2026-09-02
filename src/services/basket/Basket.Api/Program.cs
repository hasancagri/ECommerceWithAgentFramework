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

    opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
        .AutoProvision();

    // 028: OrderCreated dinleyicisi kaldirildi — sepet temizligi saga'nin gRPC adimi (ClearBasket).

    // 049: checkout sepet temizleme komutunu dinle; yanıtı orchestrator reply kuyruğuna.
    opts.ListenToRabbitQueue(RabbitMqConstants.Checkout.BasketCommandsQueue);
    opts.PublishMessage<CheckoutMessages.BasketCleared>().ToRabbitQueue(RabbitMqConstants.Checkout.RepliesQueue);

    opts.Policies.UseDurableLocalQueues();
    opts.Policies.AddMiddleware(
        typeof(Common.Utils.Authorization.ScopeAuthorizationMiddleware),
        chain => chain.MessageType.GetCustomAttribute<Common.Utils.Authorization.RequiredScopeAttribute>() is not null);
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
    // *EventHandlers static sinifi ad konvansiyonuyla otomatik kesfedilmiyor (Storefront deseni);
    // acikca dahil et — yoksa ClearBasketCommand (049) calismaz.
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

// L2 (paylaşımlı) önbellek katmanı — Redis IDistributedCache; opsiyonel (yoksa HybridCache yalnız L1).
if (builder.Configuration.GetConnectionString("redis") is not null)
    builder.AddRedisDistributedCache("redis");

// Declarative caching aspect'i: HybridCache + IMessageBus'ı şeffaf sar. UseWolverine'den sonra olmalı.
builder.Services.AddCachingAspect("basket");

builder.Services.AddHttpContextAccessor();

// 028: checkout saga ClearBasket gRPC sunucusu (Order saga'si makine token'iyla cagirir).
builder.Services.AddGrpc();

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

// 028: ClearBasket gRPC ucu; yetki endpoint seviyesinde (userId cagri govdesinde — Stock deseni).
app.MapGrpcService<Basket.Api.Grpc.BasketClearGrpcService>()
    .RequireAuthorization(AuthorizationScopes.BasketWrite);

// 039: GetBasketItems gRPC ucu (Order.Api chat siparis tamamlama; makine token'i basket.read).
app.MapGrpcService<Basket.Api.Grpc.BasketItemsGrpcService>()
    .RequireAuthorization(AuthorizationScopes.BasketRead);

await app.RunAsync();