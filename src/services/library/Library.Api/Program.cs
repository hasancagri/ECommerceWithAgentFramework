var builder = WebApplication.CreateBuilder(args);
builder.AddOpenApiDocumentation();
builder.AddServiceDefaults();

var libraryDb = builder.Configuration.GetConnectionString("libraryDb")!;
builder.Services.AddMarten(opts =>
    {
        opts.DatabaseSchemaName = SchemaConstants.LibrarySchemaName;
        opts.Connection(libraryDb);
        opts.UseNewtonsoftForSerialization(
            nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
            configure: s => s.ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor);

        // FR-002: aynı kullanıcı + ürüne tek alarm — uygulama kontrolü + sorgu index'i.
        opts.Schema.For<PriceAlarm>()
            .Index(x => x.UserId)
            .Index(x => x.ProductId);
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

    // Tüketici: Catalog'un product.changed fanout'una kendi kuyruğunu bağlar (007 dersi) + dinler.
    rabbit.DeclareExchange(RabbitMqConstants.ProductChanged.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
        e.BindQueue(RabbitMqConstants.ProductChanged.Queues.Library);
    });
    opts.ListenToRabbitQueue(RabbitMqConstants.ProductChanged.Queues.Library);

    // Tüketici: NotificationAgent'ın gönderim sonucu → NotificationRecord izi.
    rabbit.DeclareExchange(RabbitMqConstants.NotificationSent.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
        e.BindQueue(RabbitMqConstants.NotificationSent.Queues.Library);
    });
    opts.ListenToRabbitQueue(RabbitMqConstants.NotificationSent.Queues.Library);

    // Yayıncı: alarm tetiği — yalnız exchange deklare eder (binding tüketici NotificationAgent'ta).
    rabbit.DeclareExchange(RabbitMqConstants.PriceAlarmTriggered.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
    });
    opts.PublishMessage<Shared.IntegrationEvents.PriceAlarmTriggered>()
        .ToRabbitExchange(RabbitMqConstants.PriceAlarmTriggered.Exchange);

    opts.Policies.UseDurableLocalQueues();
    opts.Policies.AddMiddleware(
        typeof(Common.Utils.Authorization.ScopeAuthorizationMiddleware),
        chain => chain.MessageType.GetCustomAttribute<Common.Utils.Authorization.RequiredScopeAttribute>() is not null);
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
    // *EventHandlers (çoğul) Wolverine isim-konvansiyonunca keşfedilMEZ — elle dahil et (Reviews emsali).
    opts.Discovery.IncludeType(typeof(Library.Api.LibraryEventHandlers));
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
    AuthorizationScopes.LibraryRead,
    AuthorizationScopes.LibraryWrite);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapScalarDocumentation();

var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

app.UseAuthentication();
app.UseAuthorization();

app.AddPriceAlarmGroupEndpointExtension(apiVersionSet);

await app.RunAsync();