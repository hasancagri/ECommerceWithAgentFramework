using Wolverine.RabbitMQ;

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
    .ApplyAllDatabaseChangesOnStartup();

builder.Host.UseWolverine(opts =>
{
    // Dev: tek dugum (Solo) - leader election/node-agent koordinasyonu kapali (repo konvansiyonu).
    if (builder.Environment.IsDevelopment())
        opts.Durability.Mode = DurabilityMode.Solo;

    var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
        .AutoProvision();

    // Exchange yayıncıda tanımlanır; tüketici kuyruğu da bağlanır ki agent geç açılsa bile birikir.
    rabbit.DeclareExchange(RabbitMqConstants.SupplierProductSnapshot.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
        e.BindQueue(RabbitMqConstants.SupplierProductSnapshot.Queues.Ingestion);
    });

    opts.PublishMessage<IntegrationEvents.SupplierProductSnapshotReceived>()
        .ToRabbitExchange(RabbitMqConstants.SupplierProductSnapshot.Exchange);
});

// Feed çekimi kısa ömürlü GET: standart resilience yeterli.
builder.Services.AddHttpClient(HttpClients.Feeds);

builder.Services.AddSingleton<FeedPullService>();
builder.Services.AddHostedService<FeedScheduler>(); // ilk çekim 1 dk sonra, sonra 30 dk'da bir (config)

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapFeedEndpoints();

await app.RunAsync();