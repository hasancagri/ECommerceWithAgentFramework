using Procurement.Api.Domains.PoolProducts.Features.Commands;
using Procurement.Api.Infrastructure.Feeds;
using Procurement.Api.Seeding;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddOpenApiDocumentation();

// Havuz deposu: procurementDb / procurementManagement — kimseyle paylaşılmaz (İlke I).
var procurementDb = builder.Configuration.GetConnectionString("procurementDb")!;
builder.Services.AddMarten(opts =>
    {
        opts.DatabaseSchemaName = SchemaConstants.ProcurementSchemaName;
        opts.Connection(procurementDb);
        opts.UseNewtonsoftForSerialization(
            nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
            configure: s => s.ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor);

        // Tutarlılık sınırı barkod (R1): doküman kimliği string Barcode'dur (AggregateRoot.Id denetimde kalır).
        opts.Schema.For<PoolProduct>().Identity(x => x.Barcode);
        opts.Schema.For<Supplier>().UniqueIndex(Marten.Schema.UniqueIndexType.Computed, x => x.Code);
    })
    // Outbox: yayın mesajları havuz yazımıyla AYNI Postgres tx'inde commit edilir (013 emsali).
    .IntegrateWithWolverine()
    .ApplyAllDatabaseChangesOnStartup();

builder.Host.UseWolverine(opts =>
{
    // Dev: tek dugum (Solo) - leader election/node-agent koordinasyonu kapali (repo konvansiyonu).
    if (builder.Environment.IsDevelopment())
        opts.Durability.Mode = DurabilityMode.Solo;

    opts.Policies.UseDurableOutboxOnAllSendingEndpoints();

    var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
        .AutoProvision();

    // Yayıncı yalnız exchange'i deklare eder; kuyruk + binding TÜKETİCİDE kurulur (007 dersi:
    // yayıncı BindQueue yaparsa aynı kuyruğu farklı argümanla deklare edip 406'ya düşer).
    rabbit.DeclareExchange(RabbitMqConstants.CanonicalProduct.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
    });

    opts.PublishMessage<Shared.IntegrationEvents.CanonicalProductUpserted>()
        .ToRabbitExchange(RabbitMqConstants.CanonicalProduct.Exchange);

    opts.Policies.UseDurableLocalQueues();
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
});

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ReportApiVersions = true;
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

builder.Services.AddOptionsExt();
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

// Feed çekimi kısa ömürlü GET: standart resilience yeterli (ServiceDefaults http varsayılanları).
builder.Services.AddHttpClient(SupplierFeedClient.HttpClientName);
builder.Services.AddSingleton<SupplierFeedClient>();
builder.Services.AddSingleton<FeedPullJob>();

// Seed: supplier-a/b (Priority 1/2) + kategori eşleme tabloları (idempotent).
builder.Services.AddHostedService<ProcurementSeedHostedService>();

// 008 deseni: kalıcı zamanlayıcı Hangfire — storage aynı DB'de ayrı "hangfire" şeması.
builder.Services.AddHangfire(cfg => cfg
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(pg => pg.UseNpgsqlConnection(procurementDb),
        new PostgreSqlStorageOptions { SchemaName = "hangfire" }));
builder.Services.AddHangfireServer();

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapScalarDocumentation();

var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

app.AddPoolProductGroupEndpointExtension(apiVersionSet);
app.AddSupplierGroupEndpointExtension(apiVersionSet);

// Zamanlama açılışta idempotent kurulur; ilk çekim gecikmeli job'dır (008 deseni).
// Expression'daki CancellationToken.None yer tutucudur — gerçek token'ı koşum anında Hangfire verir.
var feedPullOptions = app.Services.GetRequiredService<FeedPullOptions>();
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<IRecurringJobManager>()
        .AddOrUpdate<FeedPullJob>("feed-pull", job => job.RunAsync(CancellationToken.None),
            feedPullOptions.PullCron);

    scope.ServiceProvider.GetRequiredService<IBackgroundJobClient>()
        .Schedule<FeedPullJob>(job => job.RunAsync(CancellationToken.None),
            TimeSpan.FromSeconds(feedPullOptions.FirstPullDelaySeconds));
}

await app.RunAsync();