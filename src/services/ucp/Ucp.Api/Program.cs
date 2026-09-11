using Shared.Grpc.ExternalOrder;
using Ucp.Api.Grpc;
using Ucp.Api.CatalogProjection;
using Ucp.Api.Discovery;
using Ucp.Api.Signatures;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddOpenApiDocumentation();

var ucpDb = builder.Configuration.GetConnectionString("ucpDb")!;
builder.Services.AddMarten(opts =>
    {
        opts.DatabaseSchemaName = SchemaConstants.UcpSchemaName;
        opts.Connection(ucpDb);
        opts.UseNewtonsoftForSerialization(
            nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
            configure: s => s.ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor);

        // UCP-facing session id (protocol string) ile hızlı arama — aggregate Marten identity'si Guid Id
        // (AggregateRoot); SessionId ayrı indeks (GET/complete/cancel {id} bununla çözülür).
        opts.Schema.For<UcpCheckoutSession>().Index(x => x.SessionId);

        // Katalog projeksiyonu (read-model; identity string ProductId). Keşif/arama + create fiyat çözümü.
        opts.Schema.For<UcpCatalogItem>();

        // Giden webhook teslim izi (US3).
        opts.Schema.For<Ucp.Api.Webhooks.OutboundDelivery>();
    })
    .IntegrateWithWolverine()
    .ApplyAllDatabaseChangesOnStartup();

builder.Host.UseWolverine(opts =>
{
    // Dev: tek düğüm (Solo) — hayalet-node koordinasyon gürültüsünü keser.
    if (builder.Environment.IsDevelopment())
        opts.Durability.Mode = DurabilityMode.Solo;

    opts.Policies.UseDurableLocalQueues();

    // Katalog projeksiyonu: product.changed + stock.changed fanout'ları TEK sıralı kuyruğa (Storefront
    // deseni; binding'i tüketici kurar — 007). Aynı UcpCatalogItem satırına eşzamanlı yazım imkansızlaşır.
    var rabbitConn = builder.Configuration.GetConnectionString("rabbitmq");
    if (rabbitConn is not null)
    {
        var rabbit = opts.UseRabbitMq(rabbitConn).AutoProvision();

        rabbit.DeclareExchange(RabbitMqConstants.ProductChanged.Exchange, e =>
        {
            e.ExchangeType = ExchangeType.Fanout;
            e.BindQueue(RabbitMqConstants.ProductChanged.Queues.Ucp);
        });
        rabbit.DeclareExchange(RabbitMqConstants.StockChanged.Exchange, e =>
        {
            e.ExchangeType = ExchangeType.Fanout;
            e.BindQueue(RabbitMqConstants.StockChanged.Queues.Ucp);
        });
        opts.ListenToRabbitQueue(RabbitMqConstants.UcpCatalogEvents.Queue).Sequential();

        // US3: sipariş-olayı webhook tetiği — order.completed + order.canceled TEK sıralı kuyruğa.
        rabbit.DeclareExchange(RabbitMqConstants.OrderCompleted.Exchange, e =>
        {
            e.ExchangeType = ExchangeType.Fanout;
            e.BindQueue(RabbitMqConstants.OrderCompleted.Queues.Ucp);
        });
        rabbit.DeclareExchange(RabbitMqConstants.OrderCanceled.Exchange, e =>
        {
            e.ExchangeType = ExchangeType.Fanout;
            e.BindQueue(RabbitMqConstants.OrderCanceled.Queues.Ucp);
        });
        opts.ListenToRabbitQueue(RabbitMqConstants.UcpOrderEvents.Queue).Sequential();
    }

    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
});

// SC-006: UCP checkout.json snake_case + koşullu alanlar (null omit). ucp-api YALNIZ UCP protokolü
// sunduğu için global politika güvenli (line_items/unit_price/selected_option_id... kanonik şema).
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    o.SerializerOptions.DefaultIgnoreCondition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ReportApiVersions = true;
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

// Kanal yetkisi: dış platform makine kimliği + scope dev.ucp.shopping.checkout (İlke V m2m).
builder.Services.AddAuthenticationAndAuthorizationExtension(
    builder.Configuration,
    AuthorizationScopes.UcpCheckout);

// Options (tip'li; IConfiguration doğrudan okuma yasak). IdentityOption OrderTokenHandler'a enjekte edilir.
builder.Services.AddOptions<IdentityOption>().BindConfiguration(nameof(IdentityOption))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<IdentityOption>(sp => sp.GetRequiredService<IOptions<IdentityOption>>().Value);

builder.Services.AddOptions<UcpPlatformOption>().BindConfiguration(nameof(UcpPlatformOption))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<UcpPlatformOption>(sp => sp.GetRequiredService<IOptions<UcpPlatformOption>>().Value);

builder.Services.AddOptions<UcpSigningOption>().BindConfiguration(nameof(UcpSigningOption))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<UcpSigningOption>(sp => sp.GetRequiredService<IOptions<UcpSigningOption>>().Value);

builder.Services.AddOptions<UcpServiceAuth>().BindConfiguration(nameof(UcpServiceAuth))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<UcpServiceAuth>(sp => sp.GetRequiredService<IOptions<UcpServiceAuth>>().Value);

builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();
builder.Services.AddHttpContextAccessor();

// US3: giden webhook HTTP istemcisi (platform inbox; dev self-signed kabul).
builder.Services.AddHttpClient("ucp-webhook")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });

// UCP → Order sanksiyonlu gRPC istemcisi (already-captured sipariş devri). Makine token'ı order.write.
builder.Services.AddTransient<OrderTokenHandler>();
var orderGrpcAddress = builder.Configuration["services:order-api:https:0"] ?? "https://order-api";
builder.Services
    .AddGrpcClient<ExternalOrder.ExternalOrderClient>(o => o.Address = new Uri(orderGrpcAddress))
    .AddHttpMessageHandler<OrderTokenHandler>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        // Dev: Aspire self-signed sertifikasını kabul et (PROD'da kaldırılır).
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapScalarDocumentation();

app.UseAuthentication();
app.UseAuthorization();

// US4: gelen checkout isteklerinde RFC 9421 imza doğrulaması (RequireSignatures bayrağıyla zorlama).
app.UseMiddleware<UcpSignatureMiddleware>();

// UCP checkout session dış-protokol REST yüzeyi (create/update/get/complete/cancel). UCP path'leri
// literaldir (/ucp/checkout_sessions) — iç api/v{n} sürümleme segmenti kullanılmaz (protokol sözleşmesi).
app.AddUcpCheckoutSessionEndpoints();

// US2: keşif profili + oauth metadata + katalog lookup/search (anonim).
app.AddUcpWellKnownEndpoints();
app.AddUcpCatalogEndpoints();

await app.RunAsync();