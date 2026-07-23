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
var discountDb = postgres.AddDatabase("discountDb");
var fileDb = postgres.AddDatabase("fileDb");
var paymentDb = postgres.AddDatabase("paymentDb");
var stockDb = postgres.AddDatabase("stockDb");
var identityDb = postgres.AddDatabase("identityDb");
var storefrontDb = postgres.AddDatabase("storefrontDb");
var ingestionDb = postgres.AddDatabase("ingestionDb");
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

var basketApi = builder.AddProject<Projects.Basket_Api>("basket-api")
    .WithReference(basketDb)
    .WithReference(rabbit)
    .WaitFor(basketDb)
    .WaitFor(rabbit);

var orderApi = builder.AddProject<Projects.Order_Api>("order-api")
    .WithReference(orderDb)
    .WithReference(rabbit)
    .WaitFor(orderDb)
    .WaitFor(rabbit);

var discountApi = builder.AddProject<Projects.Discount_Api>("discount-api")
    .WithReference(discountDb)
    .WithReference(rabbit)
    .WaitFor(discountDb)
    .WaitFor(rabbit);

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
    .WithReference(discountApi)
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
    .WithReference(discountApi)
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

// Sınır bileşeni (007): feed'i çeker, değişiklik kapısından geçen kaydı kanonik event'le yayınlar.
builder.AddProject<Projects.Supplier_Gateway>("supplier-gateway")
    .WithReference(supplierGatewayDb)
    .WithReference(supplierApi)
    .WithReference(rabbit)
    .WaitFor(supplierGatewayDb)
    .WaitFor(supplierApi)
    .WaitFor(rabbit);

// Ingestion: staging DB (ingestionDb) + MCP yazımı için domain servisleri.
builder.AddProject<Projects.IngestionAgent>("ingestion-agent")
    .WithReference(ingestionDb)
    .WithReference(supplierApi)
    .WithReference(catalogApi)
    .WithReference(stockApi)
    .WithReference(discountApi)
    .WaitFor(ingestionDb)
    .WaitFor(supplierApi)
    .WaitFor(catalogApi)
    .WaitFor(stockApi)
    .WaitFor(discountApi);

// WebApp chat widget'i orchestrator'a proxy uzerinden gider => adres cozumu icin referans.
web.WithReference(chatAgent);

await builder.Build().RunAsync();