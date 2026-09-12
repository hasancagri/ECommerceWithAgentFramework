
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

        opts.Schema.For<Payment.Api.Domains.Payments.Payment>();
    })
    .IntegrateWithWolverine()
    .ApplyAllDatabaseChangesOnStartup();

builder.Host.UseWolverine(opts =>
{
    // Dev: tek dugum (Solo) - leader election/node-agent koordinasyonu kapali; kirli kapanan
    // debug oturumlarinin hayalet-node StopRemoteAgent timeout gurultusunu kokten onler.
    if (builder.Environment.IsDevelopment())
        opts.Durability.Mode = DurabilityMode.Solo;

    // 049: checkout iki-faz ödeme komutlarını dinle; yanıtları orchestrator reply kuyruğuna yayınla.
    opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!).AutoProvision();
    opts.ListenToRabbitQueue(Shared.RabbitMqConstants.Checkout.PaymentCommandsQueue);
    opts.PublishMessage<Shared.CheckoutMessages.PaymentCharged>().ToRabbitQueue(Shared.RabbitMqConstants.Checkout.RepliesQueue);

    opts.Policies.UseDurableLocalQueues();
    opts.Policies.AddMiddleware(
        typeof(Common.Utils.Authorization.ScopeAuthorizationMiddleware),
        chain => chain.MessageType.GetCustomAttribute<Common.Utils.Authorization.RequiredScopeAttribute>() is not null);
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
    // Konvansiyonel keşif *EventHandlers sınıfını atlayabiliyor → açık kayıt (Stock emsali).
    opts.Discovery.IncludeType(typeof(Payment.Api.PaymentEventHandlers));
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

// L2 (paylaşımlı) önbellek katmanı — Redis IDistributedCache; opsiyonel (yoksa HybridCache yalnız L1).
if (builder.Configuration.GetConnectionString("redis") is not null)
    builder.AddRedisDistributedCache("redis");

// Declarative caching aspect'i: HybridCache + IMessageBus'ı şeffaf sar. UseWolverine'den sonra olmalı.
builder.Services.AddCachingAspect("payment");

builder.Services.AddHttpContextAccessor();

// 075: PG NON-3D çekim S2S (çekim sahibi Payment BC — analyze I1). Options tip'li okuma.
builder.Services.AddOptions<IdentityOption>().BindConfiguration(nameof(IdentityOption))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<IdentityOption>(sp => sp.GetRequiredService<IOptions<IdentityOption>>().Value);
builder.Services.AddOptions<SagaAuth>().BindConfiguration(nameof(SagaAuth))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<SagaAuth>(sp => sp.GetRequiredService<IOptions<SagaAuth>>().Value);
builder.Services.AddOptions<PaymentGatewayOption>().BindConfiguration(nameof(PaymentGatewayOption))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<PaymentGatewayOption>(sp => sp.GetRequiredService<IOptions<PaymentGatewayOption>>().Value);
builder.Services.AddOptions<CustomerContextOption>().BindConfiguration(nameof(CustomerContextOption));
builder.Services.AddSingleton<CustomerContextOption>(sp => sp.GetRequiredService<IOptions<CustomerContextOption>>().Value);

// 075: makine token'i (client_credentials customer.read) — S2S istemcilerinde kullanılır (HttpContext yok).
builder.Services.AddTransient<SagaTokenHandler>();

var customerHttpAddress = builder.Configuration["services:customer-api:https:0"]
    ?? builder.Configuration["services:customer-api:http:0"]
    ?? "https://customer-api";
// 075: Customer yapısal ödeme-bağlamı + merchant API key istemcileri (customer.read makine token'i).
builder.Services
    .AddHttpClient<CustomerPaymentContextClient>(c => c.BaseAddress = new Uri(customerHttpAddress.TrimEnd('/') + "/"))
    .AddHttpMessageHandler<SagaTokenHandler>();
builder.Services
    .AddHttpClient<MerchantKeyClient>(c => c.BaseAddress = new Uri(customerHttpAddress.TrimEnd('/') + "/"))
    .AddHttpMessageHandler<SagaTokenHandler>();
// 075: PaymentGateway (dış repo) NON-3D çekim istemcisi — X-Api-Key per-request (MerchantKeyClient).
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