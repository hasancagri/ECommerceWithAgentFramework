
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Snapshot deposu: supplierGatewayDb / supplierGatewayManagement — kimseyle paylaşılmaz (FR-003).
var supplierGatewayDb = builder.Configuration.GetConnectionString("supplierGatewayDb")!;
builder.Services.AddMarten(opts =>
    {
        opts.DatabaseSchemaName = SchemaConstants.SupplierGatewaySchemaName;
        opts.Connection(supplierGatewayDb);
        opts.UseNewtonsoftForSerialization(
            nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
            configure: s =>
            {
                s.ConstructorHandling = Newtonsoft.Json.ConstructorHandling.AllowNonPublicDefaultConstructor;
            });

        opts.Schema.For<FeedSnapshot>();
    })
    // 013: Wolverine transactional outbox — giden event ile snapshot AYNI Postgres tx'inde commit
    // edilir; envelope tabloları supplierGatewayDb'de kalır (bounded-context izolasyonu korunur).
    .IntegrateWithWolverine()
    .ApplyAllDatabaseChangesOnStartup();

builder.Host.UseWolverine(opts =>
{
    // Dev: tek dugum (Solo) - leader election/node-agent koordinasyonu kapali (repo konvansiyonu).
    if (builder.Environment.IsDevelopment())
        opts.Durability.Mode = DurabilityMode.Solo;

    // Giden mesajlar DURABLE outbox'tan geçsin: wolverine_outgoing_envelopes'a snapshot ile AYNI
    // tx'te yazılır, relay ajanı crash sonrası da teslim eder. Buffered (varsayılan) olsaydı mesaj
    // commit sonrası yalnız bellekte tutulur → process crash'te KAYIP (dual-write penceresi açık kalırdı).
    opts.Policies.UseDurableOutboxOnAllSendingEndpoints();

    var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
        .AutoProvision();

    // Yayıncı yalnız exchange'i deklare eder. Kuyruk + binding TÜKETİCİDE kurulur (agent):
    // kuyruğu özel DLQ argümanıyla deklare eden taraf o; burada BindQueue yapmak aynı kuyruğu
    // farklı argümanla deklare edip 406'ya (binding'siz fanout = sessiz kayıp) yol açıyordu.
    rabbit.DeclareExchange(RabbitMqConstants.SupplierProductSnapshot.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
    });

    opts.PublishMessage<IntegrationEvents.SupplierProductSnapshotReceived>()
        .ToRabbitExchange(RabbitMqConstants.SupplierProductSnapshot.Exchange);
});

// Feed çekimi kısa ömürlü GET: standart resilience yeterli.
builder.Services.AddHttpClient(HttpClients.Feeds);

builder.Services.AddSingleton<FeedPullService>();

// 008: kalıcı zamanlayıcı Hangfire — storage aynı DB'de ayrı "hangfire" şeması (Marten şemasına dokunmaz).
builder.Services.AddHangfire(cfg => cfg
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(pg => pg.UseNpgsqlConnection(supplierGatewayDb),
        new PostgreSqlStorageOptions { SchemaName = "hangfire" }));
builder.Services.AddHangfireServer();

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapFeedEndpoints();

// FR-008: pano yalnız dev'de var — auth'suz yüzey Development dışına açılmaz.
if (app.Environment.IsDevelopment())
{
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = [new Supplier.Gateway.HangfireDashboardAllowAllFilter()]
    });
}

// 008: zamanlama açılışta idempotent kurulur (FR-002); ilk çekim gecikmeli job'dır (FR-003).
// Expression'daki CancellationToken.None yer tutucudur — gerçek token'ı koşum anında Hangfire verir.
var pullCron = builder.Configuration.GetValue("Feeds:PullCron", "*/30 * * * *")!;
var firstPullDelay = TimeSpan.FromSeconds(builder.Configuration.GetValue("Feeds:FirstPullDelaySeconds", 60));

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<IRecurringJobManager>()
        .AddOrUpdate<FeedPullJob>("feed-pull", job => job.RunAsync(CancellationToken.None), pullCron);

    scope.ServiceProvider.GetRequiredService<IBackgroundJobClient>()
        .Schedule<FeedPullJob>(job => job.RunAsync(CancellationToken.None), firstPullDelay);
}

await app.RunAsync();