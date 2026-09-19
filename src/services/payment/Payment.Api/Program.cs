
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddOpenApiDocumentation();

var paymentDb = builder.Configuration.GetConnectionString("paymentDb")!;
builder.Services.AddMarten(opts =>
    {
        opts.DatabaseSchemaName = SchemaConstants.PaymentSchemaName;
        opts.Connection(paymentDb);
        opts.UseNewtonsoftForSerialization(
            nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
            configure: s =>
            {
                s.ConstructorHandling = Newtonsoft.Json.ConstructorHandling.AllowNonPublicDefaultConstructor;
            });

        // 077: hosted-CF PaymentIntent (mock Payment aggregate söküldü). TxRef unique = idempotency temeli
        // (çift callback tek sonuç); UserId index = get_my_payments + canlı-intent re-use sorgusu.
        // Sabit alias ŞART: PaymentIntent tablo adı tr-TR ToLower'da 'mt_doc_paymentıntent' (dotless ı)
        // olur; Marten'in computed-index delta eşleşmesi TABLO adındaki ı'da bozulur → var olan index'i
        // görmez → her boot recreate → 42P07. (order/basket ı'yı yalnız index ADINDA taşır, tablo adında
        // değil → idempotent.) Alias ı'yı tümden kaldırır: mt_doc_payment_intent.
        opts.Schema.For<Payment.Api.Domains.Payments.PaymentIntent>()
            .DocumentAlias("payment_intent")
            .UniqueIndex(Marten.Schema.UniqueIndexType.Computed, x => x.TxRef)
            .Index(x => x.UserId);
    })
    .IntegrateWithWolverine()
    .ApplyAllDatabaseChangesOnStartup();

builder.Host.UseWolverine(opts =>
{
    // Dev: tek dugum (Solo) - leader election/node-agent koordinasyonu kapali; kirli kapanan
    // debug oturumlarinin hayalet-node StopRemoteAgent timeout gurultusunu kokten onler.
    if (builder.Environment.IsDevelopment())
        opts.Durability.Mode = DurabilityMode.Solo;

    // CreatePaymentIntent handler'ı typed HttpClient (MerchantKeyClient/PgHostedPaymentClient,
    // AddHttpClient<T> = opaque lambda transient) inject eder; Wolverine inline codegen bunları
    // service-location ister. Varsayılan NotAllowed → 500. Order.Api ile aynı politika.
    opts.ServiceLocationPolicy = JasperFx.CodeGeneration.Model.ServiceLocationPolicy.AllowedButWarn;

    // 077: checkout Charge broker yolu SÖKÜLDÜ (PaymentCommandsQueue listen + PaymentCharged publish).
    var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!).AutoProvision();

    // 077: hosted-CF sonucu fanout → Order tüketir (binding'i tüketici kurar). Yayıncı yalnız exchange declare.
    rabbit.DeclareExchange(Shared.RabbitMqConstants.PaymentSucceeded.Exchange, e => e.ExchangeType = ExchangeType.Fanout);
    rabbit.DeclareExchange(Shared.RabbitMqConstants.PaymentFailed.Exchange, e => e.ExchangeType = ExchangeType.Fanout);
    opts.PublishMessage<Shared.IntegrationEvents.PaymentSucceeded>()
        .ToRabbitExchange(Shared.RabbitMqConstants.PaymentSucceeded.Exchange);
    opts.PublishMessage<Shared.IntegrationEvents.PaymentFailed>()
        .ToRabbitExchange(Shared.RabbitMqConstants.PaymentFailed.Exchange);

    opts.Policies.UseDurableLocalQueues();
    opts.Policies.AddMiddleware(
        typeof(Common.Utils.Authorization.ScopeAuthorizationMiddleware),
        chain => chain.MessageType.GetCustomAttribute<Common.Utils.Authorization.RequiredScopeAttribute>() is not null);
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
    // Handler/Consumer son eki taşımayan süreç sınıfı taramada keşfedilmez → açık kayıt şart.
    opts.Discovery.IncludeType(typeof(Payment.Api.Process.PaymentIntentExpiry));
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
    AuthorizationScopes.PaymentRead,
    AuthorizationScopes.PaymentWrite);
// 061: RFC 9728 keşif (metadata dokümanı + 401 challenge parametreleri) — dış agent OAuth zinciri.
// Dış-agent demeti yalnız payment.read (yazma demet dışı — data-model).
builder.Services.AddMcpResourceMetadata(builder.Configuration, "payment",
    AuthorizationScopes.PaymentRead);
// 061 logout: `logout` MCP tool'unun Identity.Server agent-logout ucuna forward client'ı.
builder.Services.AddAgentLogoutClient(builder.Configuration);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

// 077: hosted-CF ödeme yapılandırması (config[...] magic-string yasak → tip'li POCO + ValidateOnStart).
builder.Services.AddOptions<PaymentOptions>().BindConfiguration(nameof(PaymentOptions))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<PaymentOptions>(sp => sp.GetRequiredService<IOptions<PaymentOptions>>().Value);
// 077: Payment.Api makine token'ı (payment-s2s client_credentials) — Customer merchant-key S2S çağrısı.
builder.Services.AddOptions<Payment.Api.Options.SagaAuth>().BindConfiguration(nameof(Payment.Api.Options.SagaAuth))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<Payment.Api.Options.SagaAuth>(sp => sp.GetRequiredService<IOptions<Payment.Api.Options.SagaAuth>>().Value);
builder.Services.AddOptions<IdentityOption>().BindConfiguration(nameof(IdentityOption))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<IdentityOption>(sp => sp.GetRequiredService<IOptions<IdentityOption>>().Value);

// 077: Customer merchant-key S2S makine token'ı (customer.read) + PG hosted-payment istemcisi.
builder.Services.AddTransient<PaymentTokenHandler>();

var customerGrpcAddress = builder.Configuration["services:customer-api:https:0"]
    ?? builder.Configuration["services:customer-api:http:0"]
    ?? "https://customer-api";
builder.Services
    .AddGrpcClient<MerchantKeyService.MerchantKeyServiceClient>(o => o.Address = new Uri(customerGrpcAddress))
    .AddHttpMessageHandler<PaymentTokenHandler>();
builder.Services.AddScoped<MerchantKeyClient>();

// PG (dış DropShop) hosted-payment — X-Api-Key per-request (statik header YOK); tam URL istemcide (PgBaseUrl).
builder.Services.AddHttpClient<PgHostedPaymentClient>();

// L2 (paylaşımlı) önbellek katmanı — Redis IDistributedCache; opsiyonel (yoksa HybridCache yalnız L1).
if (builder.Configuration.GetConnectionString("redis") is not null)
    builder.AddRedisDistributedCache("redis");

// Declarative caching aspect'i: HybridCache + IMessageBus'ı şeffaf sar. UseWolverine'den sonra olmalı.
builder.Services.AddCachingAspect("payment");

builder.Services.AddHttpContextAccessor();
builder.Services.AddGrpc();
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

var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

app.UseAuthentication();
app.UseApiKeyAuthentication();
app.UseAuthorization();

// 077: PG callback (HMAC, scope yok) ucu.
app.AddPaymentIntentEndpoints(apiVersionSet);
// 077: hosted-CF S2S — Order.Api canlı-intent + link isteği (payment.write). REST'ten gRPC'ye taşındı.
app.MapGrpcService<PaymentIntentGrpcService>().RequireAuthorization(AuthorizationScopes.PaymentWrite);

// 061: MCP korumalı — kimliksiz istek 401 + resource_metadata challenge alır (dış agent keşfi).
app.MapMcp("/mcp").RequireAuthorization();
app.MapMcpResourceMetadata();

await app.RunAsync();