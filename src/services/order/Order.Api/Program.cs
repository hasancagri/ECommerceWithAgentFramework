var builder = WebApplication.CreateBuilder(args);
builder.AddOpenApiDocumentation();

var orderDb = builder.Configuration.GetConnectionString("orderDb")!;
builder.Services.AddMarten(opts =>
    {
        opts.DatabaseSchemaName = SchemaConstants.OrderSchemaName;
        opts.Connection(orderDb);
        opts.UseNewtonsoftForSerialization(
            nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
            configure: s =>
            {
                s.ConstructorHandling = Newtonsoft.Json.ConstructorHandling.AllowNonPublicDefaultConstructor;
            });

        opts.Schema.For<Order.Api.Domains.Orders.Order>().Index(x => x.BuyerId);
    })
    .IntegrateWithWolverine()
    .ApplyAllDatabaseChangesOnStartup();

builder.Host.UseWolverine(opts =>
{
    // Dev: tek dugum (Solo) - leader election/node-agent koordinasyonu kapali; kirli kapanan
    // debug oturumlarinin hayalet-node StopRemoteAgent timeout gurultusunu kokten onler.
    if (builder.Environment.IsDevelopment())
        opts.Durability.Mode = DurabilityMode.Solo;

    // 012: gRPC tipli client (AddGrpcClient) opaque factory'dir; Wolverine handler codegen'i inline
    // kuramaz ve service-location ister. StockCommitClientProxy CreateOrder handler'ina enjekte edilir.
    opts.ServiceLocationPolicy = JasperFx.CodeGeneration.Model.ServiceLocationPolicy.AllowedButWarn;

    // 028: OrderCreated exchange kaldirildi; sepet temizligi CheckoutSaga gRPC adimi.
    var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
        .AutoProvision();

    // 048: siparis odeme onayli tamamlaninca (CheckoutSaga pivot) Personalization'a yayinlanir.
    // Yayinci yalniz exchange deklare eder; kuyruk + binding TUKETICIDE (007 dersi).
    rabbit.DeclareExchange(RabbitMqConstants.OrderCompleted.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
    });
    opts.PublishMessage<IntegrationEvents.OrderCompleted>()
        .ToRabbitExchange(RabbitMqConstants.OrderCompleted.Exchange);

    // 049: checkout sipariş komutlarını (Create/Confirm/Cancel) dinle; yanıtları reply kuyruğuna.
    opts.ListenToRabbitQueue(RabbitMqConstants.Checkout.OrderCommandsQueue);
    // 049: chat (AlreadyCaptured) checkout'u StartCheckout ile orchestrator'a tetikler (cross-service).
    opts.PublishMessage<CheckoutMessages.StartCheckout>().ToRabbitQueue(RabbitMqConstants.Checkout.StartQueue);
    opts.PublishMessage<CheckoutMessages.OrderCreated>().ToRabbitQueue(RabbitMqConstants.Checkout.RepliesQueue);
    opts.PublishMessage<CheckoutMessages.OrderConfirmed>().ToRabbitQueue(RabbitMqConstants.Checkout.RepliesQueue);
    opts.PublishMessage<CheckoutMessages.OrderCancelled>().ToRabbitQueue(RabbitMqConstants.Checkout.RepliesQueue);

    opts.Policies.UseDurableLocalQueues();
    opts.Policies.AddMiddleware(
        typeof(Common.Utils.Authorization.ScopeAuthorizationMiddleware),
        chain => chain.MessageType.GetCustomAttribute<Common.Utils.Authorization.RequiredScopeAttribute>() is not null);
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
    // Konvansiyonel keşif *EventHandlers sınıfını atlayabiliyor → açık kayıt (Stock emsali).
    opts.Discovery.IncludeType(typeof(Order.Api.OrderEventHandlers));
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
    AuthorizationScopes.OrderRead,
    AuthorizationScopes.OrderWrite,
    // 044: satin-alma kaniti gRPC ucu reviews.write ister (R4 — ayri scope acilmaz).
    AuthorizationScopes.ReviewsWrite);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

// House-style Options: appsettings section'lari tip'li POCO'ya bagla (config[...] magic-string yasak).
builder.Services.AddOptions<IdentityOption>().BindConfiguration(nameof(IdentityOption))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<IdentityOption>(sp => sp.GetRequiredService<IOptions<IdentityOption>>().Value);
builder.Services.AddOptions<SagaAuth>().BindConfiguration(nameof(SagaAuth))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<SagaAuth>(sp => sp.GetRequiredService<IOptions<SagaAuth>>().Value);
builder.Services.AddOptions<Checkout>().BindConfiguration(nameof(Checkout))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<Checkout>(sp => sp.GetRequiredService<IOptions<Checkout>>().Value);

// 039: chat siparis tamamlama option'lari (PG cekim, Customer baglami, reconcile, correlation HMAC).
builder.Services.AddOptions<PaymentGatewayOption>().BindConfiguration(nameof(PaymentGatewayOption))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<PaymentGatewayOption>(sp => sp.GetRequiredService<IOptions<PaymentGatewayOption>>().Value);
builder.Services.AddOptions<CustomerContextOption>().BindConfiguration(nameof(CustomerContextOption));
builder.Services.AddSingleton<CustomerContextOption>(sp => sp.GetRequiredService<IOptions<CustomerContextOption>>().Value);
builder.Services.AddOptions<CheckoutReconcile>().BindConfiguration(nameof(CheckoutReconcile));
builder.Services.AddSingleton<CheckoutReconcile>(sp => sp.GetRequiredService<IOptions<CheckoutReconcile>>().Value);
builder.Services.AddOptions<CorrelationKeyOption>().BindConfiguration(nameof(CorrelationKeyOption))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<CorrelationKeyOption>(sp => sp.GetRequiredService<IOptions<CorrelationKeyOption>>().Value);

// L2 (paylaşımlı) önbellek katmanı — Redis IDistributedCache; opsiyonel (yoksa HybridCache yalnız L1).
if (builder.Configuration.GetConnectionString("redis") is not null)
    builder.AddRedisDistributedCache("redis");

// Declarative caching aspect'i: HybridCache + IMessageBus'ı şeffaf sar. UseWolverine'den sonra olmalı.
builder.Services.AddCachingAspect("order");
builder.Services.AddHttpContextAccessor();

// 039: makine token'i (order-saga client-credentials) — chat place_order gRPC/HTTP istemcilerinde kullanılır.
// (049: 028 checkout saga gRPC adımları söküldü; Stock commit/BasketClear artık orchestrator broker işi.)
builder.Services.AddTransient<SagaTokenHandler>();

var basketGrpcAddress = builder.Configuration["services:basket-api:https:0"]
    ?? builder.Configuration["services:basket-api:http:0"]
    ?? "https://basket-api";

// 039: GetBasketItems gRPC istemcisi (place_order kalem sentezi; makine token'i basket.read).
builder.Services
    .AddGrpcClient<Shared.Grpc.Basket.BasketQuery.BasketQueryClient>(o => o.Address = new Uri(basketGrpcAddress))
    .AddHttpMessageHandler<SagaTokenHandler>();
builder.Services.AddScoped<BasketItemsClientProxy>();

// 039: Customer yapisal odeme-baglami HTTP istemcisi (makine token'i customer.read; SagaTokenHandler).
var customerHttpAddress = builder.Configuration["services:customer-api:https:0"]
    ?? builder.Configuration["services:customer-api:http:0"]
    ?? "https://customer-api";
builder.Services
    .AddHttpClient<CustomerPaymentContextClient>(c => c.BaseAddress = new Uri(customerHttpAddress.TrimEnd('/') + "/"))
    .AddHttpMessageHandler<SagaTokenHandler>();

// 049: merchant API key istemcisi (charge/reconcile X-Api-Key kaynagi; customer.read makine token'i).
// Key MerchantInformation'dan cozulur -> statik config anahtari (senkron derdi) kalkti.
builder.Services
    .AddHttpClient<MerchantKeyClient>(c => c.BaseAddress = new Uri(customerHttpAddress.TrimEnd('/') + "/"))
    .AddHttpMessageHandler<SagaTokenHandler>();

// 039: PaymentGateway (dis repo) cekim/retrieve HTTP istemcisi — auth merchant API key (kullanici JWT
// degil). 049: X-Api-Key PER-REQUEST verilir (MerchantKeyClient'tan cozulur) — statik header YOK.
builder.Services.AddHttpClient<PaymentGatewayClient>((sp, c) =>
{
    var pg = sp.GetRequiredService<PaymentGatewayOption>();
    c.BaseAddress = new Uri(pg.BaseUrl.TrimEnd('/') + "/");
    c.Timeout = TimeSpan.FromSeconds(60);
});

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

// Dis tuketiciler icin opak UserKey (X-User-Key) custom auth semasi.
builder.Services.AddApiKeyAuthentication(builder.Configuration);

// 044: satin-alma kaniti gRPC sunucusu (Reviews istemci; kullanici bearer'i forward edilir).
builder.Services.AddGrpc();

var app = builder.Build();
app.MapScalarDocumentation();

var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

app.UseAuthentication();
app.UseApiKeyAuthentication();
app.UseAuthorization();

app.AddOrderGroupEndpointExtension(apiVersionSet);

// 044: sub==user_id guard'i serviste; yetki endpoint seviyesinde (reviews.write).
app.MapGrpcService<Order.Api.Grpc.OrderPurchaseGrpcService>()
    .RequireAuthorization(AuthorizationScopes.ReviewsWrite);

app.MapMcp("/mcp");

await app.RunAsync();