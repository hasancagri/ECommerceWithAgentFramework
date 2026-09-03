var builder = WebApplication.CreateBuilder(args);
builder.AddOpenApiDocumentation();
builder.AddServiceDefaults();

var reviewsDb = builder.Configuration.GetConnectionString("reviewsDb")!;
builder.Services.AddMarten(opts =>
    {
        opts.DatabaseSchemaName = SchemaConstants.ReviewsSchemaName;
        opts.Connection(reviewsDb);
        opts.UseNewtonsoftForSerialization(
            nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
            configure: s => s.ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor);

        // R9: tek-yorum kilidinin son sozu — uygulama kontrolu + unique index (cift savunma).
        opts.Schema.For<Review>()
            .UniqueIndex(Marten.Schema.UniqueIndexType.Computed, x => x.UserId, x => x.ProductId)
            .Index(x => x.ProductId);

        // 049: satın-alma kanıtı read-model (Id = "{userId:N}:{productId:N}"; eligibility PK lookup).
        opts.Schema.For<PurchasedProduct>();
    })
    .IntegrateWithWolverine()
    .ApplyAllDatabaseChangesOnStartup();

builder.Host.UseWolverine(opts =>
{
    // Dev: tek dugum (Solo) — repo konvansiyonu (hayalet-node gurultusunu onler).
    if (builder.Environment.IsDevelopment())
        opts.Durability.Mode = DurabilityMode.Solo;

    var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
        .AutoProvision();

    // Yayinci yalniz exchange'i deklare eder; kuyruk + binding TUKETICIDE (007 dersi).
    rabbit.DeclareExchange(RabbitMqConstants.ReviewSummaryChanged.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
    });

    opts.PublishMessage<Shared.IntegrationEvents.ReviewSummaryChanged>()
        .ToRabbitExchange(RabbitMqConstants.ReviewSummaryChanged.Exchange);

    // 046: moderasyon istegi ayri worker'a (RabbitMQ). Yayinci yalniz exchange deklare eder;
    // [Transactional] SubmitReview + transactional outbox → broker down olsa submit reviewsDb'ye
    // commit olur, mesaj outbox'ta bekler (fail-open, submit broker'a senkron baglanmaz).
    rabbit.DeclareExchange(RabbitMqConstants.ReviewModerationRequested.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
    });
    opts.PublishMessage<Shared.IntegrationEvents.ReviewModerationRequested>()
        .ToRabbitExchange(RabbitMqConstants.ReviewModerationRequested.Exchange);

    // 046: worker'in karari — tuketici kendi kuyrugunu deklare edilen exchange'e baglar (007) + dinler.
    rabbit.DeclareExchange(RabbitMqConstants.ReviewModerated.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
        e.BindQueue(RabbitMqConstants.ReviewModerated.Queues.Reviews);
    });
    opts.ListenToRabbitQueue(RabbitMqConstants.ReviewModerated.Queues.Reviews);

    // 049: Order 'OrderCompleted' tüketilir → satın-alma kanıtı read-model. Tüketici kendi kuyruğunu
    // deklare edilen exchange'e bağlar (007) + dinler. Durable → Reviews kapalıyken kaybolmaz.
    rabbit.DeclareExchange(RabbitMqConstants.OrderCompleted.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
        e.BindQueue(RabbitMqConstants.OrderCompleted.Queues.Reviews);
    });
    opts.ListenToRabbitQueue(RabbitMqConstants.OrderCompleted.Queues.Reviews);

    opts.Policies.UseDurableLocalQueues();
    opts.Policies.AddMiddleware(
        typeof(Common.Utils.Authorization.ScopeAuthorizationMiddleware),
        chain => chain.MessageType.GetCustomAttribute<Common.Utils.Authorization.RequiredScopeAttribute>() is not null);
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
    // *EventHandlers (çoğul) Wolverine isim-konvansiyonunca keşfedilMEZ — elle dahil et
    // (Catalog/Storefront/Stock emsali).
    opts.Discovery.IncludeType(typeof(Reviews.Api.ReviewsEventHandlers));
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
    AuthorizationScopes.ReviewsWrite);
// 064: RFC 9728 keşif (metadata + 401 challenge) — dış agent yorum MCP'si (get_reviews/eligibility/submit).
builder.Services.AddMcpResourceMetadata(builder.Configuration, "reviews", AuthorizationScopes.ReviewsWrite);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();
builder.Services.AddHttpContextAccessor();

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapScalarDocumentation();

var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

app.UseAuthentication();
app.UseAuthorization();

app.AddReviewGroupEndpointExtension(apiVersionSet);

// 064: MCP korumalı — kimliksiz istek 401 + resource_metadata challenge (dış agent keşfi).
// get_reviews login yeter (RequiredScope yok); eligibility/submit reviews.write (Wolverine middleware).
app.MapMcp("/mcp").RequireAuthorization();
app.MapMcpResourceMetadata();

await app.RunAsync();
