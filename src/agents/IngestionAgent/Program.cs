using System.Reflection;
using IngestionAgent.Workflows._01_CatalogWrite;
using IngestionAgent.Workflows._02_StockWrite;
using IngestionAgent.Workflows._03_DiscountWrite;
using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// 007: agent DB'siz saf yönlendiricidir (FR-017): mesaj al → workflow → MCP. Kalıcı iz tutulmaz;
// görünürlük kuyruk derinliği + DLQ + loglardır (run API'si öldü).
builder.Host.UseWolverine(opts =>
{
    // Dev: tek dugum (Solo) - leader election/node-agent koordinasyonu kapali (repo konvansiyonu).
    if (builder.Environment.IsDevelopment())
        opts.Durability.Mode = DurabilityMode.Solo;

    var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
        .AutoProvision();

    // Binding'i TÜKETİCİ kurar: kuyruğu DLQ argümanlarıyla deklare eden taraf da o. Yayıncı
    // tarafı BindQueue yaparsa aynı kuyruğu farklı argümanla deklare eder → 406, binding kurulmaz.
    rabbit.DeclareExchange(RabbitMqConstants.SupplierProductSnapshot.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
        e.BindQueue(RabbitMqConstants.SupplierProductSnapshot.Queues.Ingestion);
    });

    // Inline tüketim: ack ancak handler başarısında → agent DB'siz de at-least-once korunur.
    // DLQ adı RabbitMqConstants'tan: retry tükenince mesaj İÇERİĞİYLE buraya düşer (FR-019/020).
    opts.ListenToRabbitQueue(RabbitMqConstants.SupplierProductSnapshot.Queues.Ingestion)
        .ProcessInline()
        .DeadLetterQueueing(new DeadLetterQueue(RabbitMqConstants.SupplierProductSnapshot.DeadLetter));

    // Kademeli sınırlı retry (R6): geçici hata (servis kapalı) pencere içinde kurtulur;
    // ısrarcı hata 3 denemede tükenir → MoveToErrorQueue → DLQ. Sonsuz retry bilinçli yok.
    opts.OnException<IngestionWriteException>()
        .RetryWithCooldown(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60))
        .Then.MoveToErrorQueue();

    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
    // Konvansiyonel keşif handler'ı atlayabiliyor (Storefront emsali); açık kayıt garantili yol.
    opts.Discovery.IncludeType(typeof(SupplierSnapshotHandler));
});

// MCP client: tokensiz (yazma yolu anonim, R5). MCP'nin uzun ömürlü SSE bağlantısını standart
// resilience handler'ı kesip keşfi çökertir → muaf tutulur (ChatAgent emsali).
#pragma warning disable EXTEXP0001 // RemoveAllResilienceHandlers experimental; MCP için gerekli
builder.Services.AddHttpClient(HttpClients.McpNoToken)
    .RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001

// Yazıcı agent'lar Singleton (konvansiyon): her biri kendi MCP bağlantısını içinde taşır (FR-016).
// Bağlantı TEMBEL — açılışta değil ilk tool çağrısında kurulur (startup, hedef servis hazır
// değil diye ölmez). Tool'lar DOĞRUDAN çağrılır (LLM yok); adresler Aspire service discovery'den.
var catalogMcp = $"{builder.Configuration["services:catalog-api:http:0"]}/mcp";
var stockMcp = $"{builder.Configuration["services:stock-api:http:0"]}/mcp";
var discountMcp = $"{builder.Configuration["services:discount-api:http:0"]}/mcp";

McpConnection Connection(IServiceProvider sp, string name, string url) =>
    new(sp.GetRequiredService<IHttpClientFactory>(), name, url);

builder.Services.AddSingleton<CatalogWriterAgent>(sp => new(Connection(sp, "catalog", catalogMcp)));
builder.Services.AddSingleton<StockWriterAgent>(sp => new(Connection(sp, "stock", stockMcp)));
builder.Services.AddSingleton<DiscountWriterAgent>(sp => new(Connection(sp, "discount", discountMcp)));

var app = builder.Build();

app.MapDefaultEndpoints(); // HTTP yüzeyi yalnız health (quickstart S7)

await app.RunAsync();