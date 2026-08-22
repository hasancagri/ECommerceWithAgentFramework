using Reviews.Api.Infrastructure.Moderation;

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
    })
    .IntegrateWithWolverine()
    .ApplyAllDatabaseChangesOnStartup();

builder.Host.UseWolverine(opts =>
{
    // Dev: tek dugum (Solo) — repo konvansiyonu (hayalet-node gurultusunu onler).
    if (builder.Environment.IsDevelopment())
        opts.Durability.Mode = DurabilityMode.Solo;

    // 012 dersi: gRPC tipli client (AddGrpcClient) opaque factory — handler codegen service-location ister.
    opts.ServiceLocationPolicy = JasperFx.CodeGeneration.Model.ServiceLocationPolicy.AllowedButWarn;

    var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
        .AutoProvision();

    // Yayinci yalniz exchange'i deklare eder; kuyruk + binding TUKETICIDE (007 dersi).
    rabbit.DeclareExchange(RabbitMqConstants.ReviewSummaryChanged.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
    });

    opts.PublishMessage<Shared.IntegrationEvents.ReviewSummaryChanged>()
        .ToRabbitExchange(RabbitMqConstants.ReviewSummaryChanged.Exchange);

    // FR-012: moderasyon lokal durable kuyrugu — yayin AI gecikmesine baglanmaz (fail-open).
    opts.PublishMessage<ModerateReview.ModerateReviewCommand>()
        .ToLocalQueue(ModerateReview.QueueName);
    opts.OnException<ModerationException>()
        .RetryWithCooldown(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60))
        .Then.MoveToErrorQueue();

    opts.Policies.UseDurableLocalQueues();
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
    AuthorizationScopes.ReviewsWrite);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();
builder.Services.AddHttpContextAccessor();

// Moderasyon config'i ZORUNLU — acilista fail-fast (EnrichmentOptions emsali; section "OpenAI").
builder.Services.AddOptions<Reviews.Api.Options.ModerationOptions>()
    .BindConfiguration(Reviews.Api.Options.ModerationOptions.SectionName)
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<Reviews.Api.Options.ModerationOptions>(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Reviews.Api.Options.ModerationOptions>>().Value);

// Agent Singleton'dir (konvansiyon: framework baslangicta yakalar).
builder.Services.AddSingleton<ModerationAgent>();

// 044: Order satin-alma kaniti gRPC istemcisi; kullanici bearer'i BearerForwardingHandler ile tasinir.
builder.Services.AddTransient<Reviews.Api.Grpc.BearerForwardingHandler>();
// gRPC balancer'inin Aspire service-discovery cozumleyicisi YOK — somut adres (012 emsali).
var orderGrpcAddress = builder.Configuration["services:order-api:https:0"]
    ?? builder.Configuration["services:order-api:http:0"]
    ?? "https://order-api";
builder.Services
    .AddGrpcClient<OrderPurchase.OrderPurchaseClient>(o => o.Address = new Uri(orderGrpcAddress))
    .AddHttpMessageHandler<Reviews.Api.Grpc.BearerForwardingHandler>();
// Proxy somut tipiyle kaydedilir (Scrutor marker'i somut cozumu vermez — Basket emsali).
builder.Services.AddScoped<OrderPurchaseClientProxy>();

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

await app.RunAsync();
