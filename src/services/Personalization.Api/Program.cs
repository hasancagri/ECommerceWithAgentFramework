var builder = WebApplication.CreateBuilder(args);
builder.AddOpenApiDocumentation();
builder.AddServiceDefaults();

var personalizationApiDb = builder.Configuration.GetConnectionString("personalizationApiDb")!;
builder.Services.AddMarten(opts =>
    {
        opts.DatabaseSchemaName = SchemaConstants.PersonalizationApiSchemaName;
        opts.Connection(personalizationApiDb);
        opts.UseNewtonsoftForSerialization(
            nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
            configure: s => s.ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor);

        // 048: satin-alma sinyali — Id=OrderId (idempotent); kullanici-bazli gelecek okuma icin index.
        opts.Schema.For<PurchaseSignal>()
            .Index(x => x.UserId);

        // 048: gezinme telemetrisi — kullanici/anonim bazli gelecek okuma icin index.
        opts.Schema.For<BehaviorSignal>()
            .Index(x => x.UserId)
            .Index(x => x.AnonymousId);
    })
    .IntegrateWithWolverine()
    .ApplyAllDatabaseChangesOnStartup();

builder.Host.UseWolverine(opts =>
{
    if (builder.Environment.IsDevelopment())
        opts.Durability.Mode = DurabilityMode.Solo;

    var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
        .AutoProvision();

    // 048: tuketici kendi kuyrugunu deklare edilen exchange'e baglar (007 dersi) + dinler.
    rabbit.DeclareExchange(RabbitMqConstants.OrderCompleted.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
        e.BindQueue(RabbitMqConstants.OrderCompleted.Queues.Personalization);
    });
    opts.ListenToRabbitQueue(RabbitMqConstants.OrderCompleted.Queues.Personalization);

    opts.Policies.UseDurableLocalQueues();
    opts.Policies.AddMiddleware(
        typeof(Common.Utils.Authorization.ScopeAuthorizationMiddleware),
        chain => chain.MessageType.GetCustomAttribute<Common.Utils.Authorization.RequiredScopeAttribute>() is not null);
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
    // *EventHandlers (çoğul) Wolverine isim-konvansiyonunca keşfedilMEZ — elle dahil et (Reviews emsali).
    opts.Discovery.IncludeType(typeof(Personalization.Api.PersonalizationEventHandlers));
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
    AuthorizationScopes.PersonalizationIngest);
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

app.AddSignalGroupEndpointExtension(apiVersionSet);

await app.RunAsync();