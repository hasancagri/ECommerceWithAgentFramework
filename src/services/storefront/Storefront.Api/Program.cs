var builder = WebApplication.CreateBuilder(args);
builder.AddOpenApiDocumentation();
builder.AddServiceDefaults();

var storefrontDb = builder.Configuration.GetConnectionString("storefrontDb")!;
builder.Services.AddMarten(opts =>
    {
        opts.DatabaseSchemaName = SchemaConstants.StorefrontSchemaName;
        opts.Connection(storefrontDb);
        opts.UseNewtonsoftForSerialization(
            nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
            configure: s => s.ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor);

        // Rich aggregate degil (invariant tasimaz); ProductId, Marten Id'si. Tek composite satir.
        // Optimistic concurrency: farkli kaynaklarin ayni satira eszamanli yazmasinda lost-update
        // olmaz — cakisan handler ConcurrencyException alir, Wolverine retry'da taze yukleyip uygular.
        opts.Schema.For<StorefrontView>().Identity(x => x.ProductId).UseOptimisticConcurrency(true);
    })
    .IntegrateWithWolverine()
    .ApplyAllDatabaseChangesOnStartup();

builder.Host.UseWolverine(opts =>
{
    opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
        .AutoProvision();

    opts.ListenToRabbitQueue(RabbitMqConstants.ProductChanged.Queues.Storefront).Sequential();
    opts.ListenToRabbitQueue(RabbitMqConstants.StockChanged.Queues.Storefront).Sequential();
    opts.ListenToRabbitQueue(RabbitMqConstants.DiscountChanged.Queues.Storefront).Sequential();

    // Composite satirda kaynaklar-arasi eszamanli yazim cakismasi (optimistic concurrency) → retry.
    opts.OnException<JasperFx.ConcurrencyException>().RetryTimes(5);
    opts.Policies.UseDurableLocalQueues();
    // Handler-level yetki: middleware SADECE [RequiredScope] tasiyan komut/sorgulara weave edilir.
    // REST + MCP ortak yetki noktasi.
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
    AuthorizationScopes.StorefrontRead);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

builder.Services.AddHttpContextAccessor();
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

// Dis tuketiciler icin opak UserKey (X-User-Key) custom auth semasi.
builder.Services.AddApiKeyAuthentication(builder.Configuration);

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapScalarDocumentation();

var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

app.UseAuthentication();
app.UseApiKeyAuthentication();
app.UseAuthorization();

app.AddStorefrontViewGroupEndpointExtension(apiVersionSet);

app.MapMcp("/mcp");

await app.RunAsync();