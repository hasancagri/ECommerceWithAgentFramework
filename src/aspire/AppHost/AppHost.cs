var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

var rabbit = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin()
    .WithLifetime(ContainerLifetime.Persistent);

// L2 (paylaşımlı) önbellek katmanı — HybridCache'in IDistributedCache backing'i (opsiyonel).
var redis = builder.AddRedis("redis")
    .WithLifetime(ContainerLifetime.Persistent);

var catalogDb = postgres.AddDatabase("catalogDb");
var basketDb = postgres.AddDatabase("basketDb");
var orderDb = postgres.AddDatabase("orderDb");
var fileDb = postgres.AddDatabase("fileDb");
var paymentDb = postgres.AddDatabase("paymentDb");
var stockDb = postgres.AddDatabase("stockDb");
var identityDb = postgres.AddDatabase("identityDb");
var storefrontDb = postgres.AddDatabase("storefrontDb");
var supplierGatewayDb = postgres.AddDatabase("supplierGatewayDb");

var identityServer = builder.AddProject<Projects.Identity_Server>("identity-server")
    .WithReference(identityDb)
    .WaitFor(identityDb);

var catalogApi = builder.AddProject<Projects.Catalog_Api>("catalog-api")
    .WithReference(catalogDb)
    .WithReference(rabbit)
    .WithReference(redis)
    .WaitFor(catalogDb)
    .WaitFor(rabbit)
    .WaitFor(redis);

var stockApi = builder.AddProject<Projects.Stock_Api>("stock-api")
    .WithReference(stockDb)
    .WithReference(rabbit)
    .WaitFor(stockDb)
    .WaitFor(rabbit);

// 012: Basket & Order, Stock'a senkron gRPC (rezervasyon Reserve/Release/Commit) çağırır.
var basketApi = builder.AddProject<Projects.Basket_Api>("basket-api")
    .WithReference(basketDb)
    .WithReference(rabbit)
    .WithReference(stockApi)
    .WaitFor(basketDb)
    .WaitFor(rabbit)
    .WaitFor(stockApi);

var orderApi = builder.AddProject<Projects.Order_Api>("order-api")
    .WithReference(orderDb)
    .WithReference(rabbit)
    .WithReference(stockApi)
    .WaitFor(orderDb)
    .WaitFor(rabbit)
    .WaitFor(stockApi);

var fileApi = builder.AddProject<Projects.File_Api>("file-api")
    .WithReference(fileDb)
    .WithReference(rabbit)
    .WaitFor(fileDb)
    .WaitFor(rabbit);

var storefrontApi = builder.AddProject<Projects.Storefront_Api>("storefront-api")
    .WithReference(storefrontDb)
    .WithReference(rabbit)
    .WithReference(identityServer)
    .WaitFor(storefrontDb)
    .WaitFor(rabbit)
    .WaitFor(identityServer);

var paymentApi = builder.AddProject<Projects.Payment_Api>("payment-api")
    .WithReference(paymentDb)
    .WaitFor(paymentDb);

var gateway = builder.AddProject<Projects.Gateway>("gateway")
    .WithReference(catalogApi)
    .WithReference(basketApi)
    .WithReference(orderApi)
    .WithReference(paymentApi)
    .WithReference(stockApi)
    .WithReference(fileApi)
    .WithReference(storefrontApi)
    .WithReference(identityServer)
    .WaitFor(identityServer);

var web = builder.AddProject<Projects.WebApp>("ecommerce-web");
web.WithReference(basketApi)
    .WithReference(catalogApi)
    .WithReference(stockApi)
    .WithReference(orderApi)
    .WithReference(fileApi)
    .WithReference(paymentApi)
    .WithReference(storefrontApi)
    .WithReference(identityServer)
    .WaitFor(identityServer);


var chatAgent = builder.AddProject<Projects.ChatAgent>("chat-agent")
    .WithReference(gateway)
    .WaitFor(gateway);

// Tedarikçi simülatörü: DB'siz (dataset dosyalarını istek anında okur — 005/R12).
var supplierApi = builder.AddProject<Projects.Supplier_Api>("supplier-api");

// Ingestion (007): DB'siz saf tüketici — kuyruktan okur, MCP ile domain servislerine yazar.
// Kuyruk + binding'i (DLQ argümanlı) provision eden TARAF budur; gateway bunu bekler (aşağıda).
var ingestionAgent = builder.AddProject<Projects.IngestionAgent>("ingestion-agent")
    .WithReference(rabbit)
    .WithReference(catalogApi)
    .WithReference(stockApi)
    .WaitFor(rabbit)
    .WaitFor(catalogApi)
    .WaitFor(stockApi);

// Sınır bileşeni (007): feed'i çeker, değişiklik kapısından geçen kaydı kanonik event'le yayınlar.
// 012: WaitFor(ingestionAgent) — tüketici kuyruğu provision etmeden feed yayınlanmasın; yoksa
// soğuk açılışta ilk publish bağlı-kuyruksuz fanout'a düşer (sessiz kayıp → snapshot dolu, Catalog boş).
builder.AddProject<Projects.Supplier_Gateway>("supplier-gateway")
    .WithReference(supplierGatewayDb)
    .WithReference(supplierApi)
    .WithReference(rabbit)
    .WaitFor(supplierGatewayDb)
    .WaitFor(supplierApi)
    .WaitFor(rabbit)
    .WaitFor(ingestionAgent);

// WebApp chat widget'i orchestrator'a proxy uzerinden gider => adres cozumu icin referans.
web.WithReference(chatAgent);

await builder.Build().RunAsync();