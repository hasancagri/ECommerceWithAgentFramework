var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// 007: agent DB'siz saf yönlendiricidir: mesaj al → workflow → MCP. Kalıcı iz tutulmaz;
// görünürlük kuyruk derinliği + DLQ + loglardır. 015: yazma adımları LLM-sürücülüdür.
builder.Host.UseWolverine(opts =>
{
    // Dev: tek dugum (Solo) - leader election/node-agent koordinasyonu kapali (repo konvansiyonu).
    if (builder.Environment.IsDevelopment())
        opts.Durability.Mode = DurabilityMode.Solo;

    // 015/R5 (016: zincir 5 adıma çıktı): mesaj bütçesi 5 LLM adımının toplam üst sınırının
    // (5×60s) ÜSTÜNDE olmalı; yoksa Wolverine'in varsayılan 60s'i adım ortasında dış iptal üretir
    // → S4'ün kapattığı belirsiz yol geri gelir. Adım-içi timeout'lar (StepTimeoutSeconds) asıl
    // bekçidir; bu, son emniyettir.
    opts.DefaultExecutionTimeout = TimeSpan.FromMinutes(6);

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
    // DLQ adı RabbitMqConstants'tan: retry tükenince mesaj İÇERİĞİYLE buraya düşer (FR-004).
    opts.ListenToRabbitQueue(RabbitMqConstants.SupplierProductSnapshot.Queues.Ingestion)
        .ProcessInline()
        .DeadLetterQueueing(new DeadLetterQueue(RabbitMqConstants.SupplierProductSnapshot.DeadLetter));

    // Kademeli sınırlı retry: geçici hata (servis/model kapalı) pencere içinde kurtulur;
    // ısrarcı hata 3 denemede tükenir → MoveToErrorQueue → DLQ. Sonsuz retry bilinçli yok.
    opts.OnException<IngestionWriteException>()
        .RetryWithCooldown(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60))
        .Then.MoveToErrorQueue();

    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
    // Konvansiyonel keşif handler'ı atlayabiliyor (Storefront emsali); açık kayıt garantili yol.
    opts.Discovery.IncludeType(typeof(SupplierSnapshotHandler));
});

// FR-014 fail-fast: model config'i ZORUNLU; model default'u bilinçli YOK (R7 — yazma yolunda
// örtük model sürüklenmesi istenmez). Kaynak: user-secrets/appsettings (ChatAgent mekanizması).
string openAiKey = builder.Configuration["OpenAI:ApiKey"]
    ?? throw new InvalidOperationException("OpenAI:ApiKey yapılandırılmamış (user-secrets/appsettings)");
string openAiModel = builder.Configuration["OpenAI:Model"]
    ?? throw new InvalidOperationException("OpenAI:Model yapılandırılmamış (user-secrets/appsettings)");

// Tek paylaşılan chat istemcisi (FR-010): beş yazıcı agent da aynı modeli kullanır.
IChatClient chatClient = new OpenAIClient(openAiKey)
    .GetChatClient(openAiModel)
    .AsIChatClient()
    .AsBuilder()
    .ConfigureOptions(o => o.ModelId = openAiModel)
    .Build();

// MCP client: tokensiz (yazma yolu anonim, FR-008). MCP'nin uzun ömürlü SSE bağlantısını standart
// resilience handler'ı kesip keşfi çökertir → muaf tutulur (ChatAgent emsali).
#pragma warning disable EXTEXP0001 // RemoveAllResilienceHandlers experimental; MCP için gerekli
builder.Services.AddHttpClient(McpClients.NoToken)
    .RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001

// Yazıcı agent'lar Singleton (konvansiyon): her biri KENDİ servisinin izinli tool'larına scope'lu
// LLM agent'ı taşır (FR-009). Tool keşfi TEMBEL — açılışta değil ilk mesajda (hedef servis hazır
// değil diye ölmez). Adresler Aspire service discovery'den.
var catalogMcp = $"{builder.Configuration["services:catalog-api:http:0"]}/mcp";
var stockMcp = $"{builder.Configuration["services:stock-api:http:0"]}/mcp";

// R5: adım bütçesi (keşif + LLM döngüsü + tool çağrıları); taşma adım hatası → retry/DLQ.
var stepTimeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("Ingestion:StepTimeoutSeconds", 60));

McpToolCatalog Catalog(IServiceProvider sp, string name, string url, string[] allowedTools) =>
    new(sp.GetRequiredService<IHttpClientFactory>(), name, url, allowedTools,
        sp.GetRequiredService<ILogger<McpToolCatalog>>());

// 016 R10: marka/kategori yazıcıları da catalog MCP'sine bağlanır; her biri yalnız kendi tool'unu görür.
builder.Services.AddSingleton<BrandWriterAgent>(sp => new(
    chatClient, Catalog(sp, Writers.Brand, catalogMcp, BrandWriterAgent.AllowedTools), stepTimeout));
builder.Services.AddSingleton<CategoryWriterAgent>(sp => new(
    chatClient, Catalog(sp, Writers.Category, catalogMcp, CategoryWriterAgent.AllowedTools), stepTimeout));
builder.Services.AddSingleton<CatalogWriterAgent>(sp => new(
    chatClient, Catalog(sp, Writers.Catalog, catalogMcp, CatalogWriterAgent.AllowedTools), stepTimeout));
builder.Services.AddSingleton<StockWriterAgent>(sp => new(
    chatClient, Catalog(sp, Writers.Stock, stockMcp, StockWriterAgent.AllowedTools), stepTimeout));

var app = builder.Build();

app.MapDefaultEndpoints(); // HTTP yüzeyi yalnız health

await app.RunAsync();