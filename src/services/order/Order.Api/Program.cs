var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
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
    // 049/077: hosted-CF ödeme başarılı → StartCheckout (AlreadyCaptured) orchestrator'a (cross-service).
    opts.PublishMessage<CheckoutMessages.StartCheckout>().ToRabbitQueue(RabbitMqConstants.Checkout.StartQueue);
    opts.PublishMessage<CheckoutMessages.OrderCreated>().ToRabbitQueue(RabbitMqConstants.Checkout.RepliesQueue);
    opts.PublishMessage<CheckoutMessages.OrderConfirmed>().ToRabbitQueue(RabbitMqConstants.Checkout.RepliesQueue);
    opts.PublishMessage<CheckoutMessages.OrderCancelled>().ToRabbitQueue(RabbitMqConstants.Checkout.RepliesQueue);

    // 077: Payment.Api hosted-CF sonuç fanout'ları — tüketici kendi kuyruğunu bağlar + dinler (007 dersi).
    rabbit.DeclareExchange(RabbitMqConstants.PaymentSucceeded.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
        e.BindQueue(RabbitMqConstants.PaymentSucceeded.Queues.Order);
    });
    opts.ListenToRabbitQueue(RabbitMqConstants.PaymentSucceeded.Queues.Order);
    rabbit.DeclareExchange(RabbitMqConstants.PaymentFailed.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
        e.BindQueue(RabbitMqConstants.PaymentFailed.Queues.Order);
    });
    opts.ListenToRabbitQueue(RabbitMqConstants.PaymentFailed.Queues.Order);

    // 049/074: checkout sağası step-komut tüketiminde altyapı hatası retry (Checkout.Orchestrator'daki
    // policyle aynı — FR-024). İş hatası (Result.Permanent) bunu tetiklemez, yalnız fırlayan exception.
    opts.OnException<Exception>().RetryWithCooldown(
        TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15));

    opts.Policies.UseDurableLocalQueues();
    opts.Policies.AddMiddleware(
        typeof(Common.Utils.Authorization.ScopeAuthorizationMiddleware),
        chain => chain.MessageType.GetCustomAttribute<Common.Utils.Authorization.RequiredScopeAttribute>() is not null);
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
    // Konvansiyonel keşif *EventHandlers/*Consumers sınıfını atlayabiliyor → açık kayıt (Stock emsali).
    opts.Discovery.IncludeType(typeof(Order.Api.Saga.CheckoutConsumers));
    opts.Discovery.IncludeType(typeof(Order.Api.PaymentConsumers));
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
// 061: RFC 9728 keşif (metadata dokümanı + 401 challenge parametreleri) — dış agent OAuth zinciri.
builder.Services.AddMcpResourceMetadata(builder.Configuration, "order",
    AuthorizationScopes.OrderRead, AuthorizationScopes.OrderWrite);
// 061 logout: `logout` MCP tool'unun Identity.Server agent-logout ucuna forward client'ı.
builder.Services.AddAgentLogoutClient(builder.Configuration);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

// House-style Options: appsettings section'lari tip'li POCO'ya bagla (config[...] magic-string yasak).
builder.Services.AddOptions<IdentityOption>().BindConfiguration(nameof(IdentityOption))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<IdentityOption>(sp => sp.GetRequiredService<IOptions<IdentityOption>>().Value);
builder.Services.AddOptions<Checkout>().BindConfiguration(nameof(Checkout))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<Checkout>(sp => sp.GetRequiredService<IOptions<Checkout>>().Value);
// 077: hosted-CF start_payment makine token'ı (order-saga; basket.read + customer.read + payment.write).
builder.Services.AddOptions<Order.Api.Options.SagaAuth>().BindConfiguration(nameof(Order.Api.Options.SagaAuth))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<Order.Api.Options.SagaAuth>(sp => sp.GetRequiredService<IOptions<Order.Api.Options.SagaAuth>>().Value);

// L2 (paylaşımlı) önbellek katmanı — Redis IDistributedCache; opsiyonel (yoksa HybridCache yalnız L1).
if (builder.Configuration.GetConnectionString("redis") is not null)
    builder.AddRedisDistributedCache("redis");

// Declarative caching aspect'i: HybridCache + IMessageBus'ı şeffaf sar. UseWolverine'den sonra olmalı.
builder.Services.AddCachingAspect("order");
builder.Services.AddHttpContextAccessor();

// 077: hosted-CF start_payment S2S yolu — makine token (order-saga) + sepet gRPC + Payment/Customer istemcileri.
builder.Services.AddTransient<SagaTokenHandler>();

var basketGrpcAddress = builder.Configuration["services:basket-api:https:0"]
    ?? builder.Configuration["services:basket-api:http:0"]
    ?? "https://basket-api";
builder.Services
    .AddGrpcClient<Shared.Grpc.Basket.BasketQuery.BasketQueryClient>(o => o.Address = new Uri(basketGrpcAddress))
    .AddHttpMessageHandler<SagaTokenHandler>();
builder.Services.AddScoped<BasketItemsClientProxy>();

var paymentGrpcAddress = builder.Configuration["services:payment-api:https:0"]
    ?? builder.Configuration["services:payment-api:http:0"]
    ?? "https://payment-api";
builder.Services
    .AddGrpcClient<PaymentIntentService.PaymentIntentServiceClient>(o => o.Address = new Uri(paymentGrpcAddress))
    .AddHttpMessageHandler<SagaTokenHandler>();
builder.Services.AddScoped<PaymentIntentClient>();

var customerGrpcAddress = builder.Configuration["services:customer-api:https:0"]
    ?? builder.Configuration["services:customer-api:http:0"]
    ?? "https://customer-api";
builder.Services
    .AddGrpcClient<Shared.Grpc.Customer.AddressQuery.AddressQueryClient>(o => o.Address = new Uri(customerGrpcAddress))
    .AddHttpMessageHandler<SagaTokenHandler>();
builder.Services.AddScoped<AddressClient>();

// 079: Discount.Api checkout gRPC istemcisi — start_payment aktif indirim yüzdesini canlı sorar (discount.read).
var discountGrpcAddress = builder.Configuration["services:discount-api:https:0"]
    ?? builder.Configuration["services:discount-api:http:0"]
    ?? "https://discount-api";
builder.Services
    .AddGrpcClient<DiscountQuery.DiscountQueryClient>(o => o.Address = new Uri(discountGrpcAddress))
    .AddHttpMessageHandler<SagaTokenHandler>();
builder.Services.AddScoped<DiscountClient>();

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

// Dis tuketiciler icin opak UserKey (X-User-Key) custom auth semasi.
builder.Services.AddApiKeyAuthentication(builder.Configuration);

var app = builder.Build();
// AppHost WithHttpHealthCheck("/health") bu ucu yoklar (Development-only map).
app.MapDefaultEndpoints();
app.MapScalarDocumentation();

app.UseAuthentication();
app.UseApiKeyAuthentication();
app.UseAuthorization();


// 061: MCP korumalı — kimliksiz istek 401 + resource_metadata challenge alır (dış agent keşfi).
app.MapMcp("/mcp").RequireAuthorization();
app.MapMcpResourceMetadata();

await app.RunAsync();