using IngestionAgent.Workflows._02_DomainWrite.Agents;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Staging deposu: ingestionDb / ingestionManagement (feature'ın tek yeni DB'si).
// Wolverine YOK (plan kararı) → Marten doğrudan kullanılır; yazımlar LightweightSession ile.
var ingestionDb = builder.Configuration.GetConnectionString("ingestionDb")!;
builder.Services.AddMarten(opts =>
    {
        opts.DatabaseSchemaName = SchemaConstants.IngestionSchemaName;
        opts.Connection(ingestionDb);
        opts.UseNewtonsoftForSerialization(
            nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
            configure: s =>
            {
                s.ConstructorHandling = Newtonsoft.Json.ConstructorHandling.AllowNonPublicDefaultConstructor;
            });

        opts.Schema.For<StagingRecord>();
        opts.Schema.For<IngestionRun>();
    })
    .ApplyAllDatabaseChangesOnStartup();

// Feed client: standart resilience kalır (feed çekimi kısa ömürlü GET).
builder.Services.AddHttpClient(HttpClients.Feeds);

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

builder.Services.AddSingleton<IngestionRunService>();
builder.Services.AddHostedService<IngestionScheduler>(); // 30 dk'da bir otomatik run

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapIngestionEndpoints();

await app.RunAsync();