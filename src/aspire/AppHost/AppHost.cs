var builder = DistributedApplication.CreateBuilder(args);

// 019: pgvector'lu resmi imaj. pg17 = Aspire default'u (postgres:17.x) ile ayni veri yolu; mevcut
// volume uyumlu. pg18 tag'i KULLANMA (WithDataVolume tag'i parse edemez, 17-yolunu mount eder).
// WithImage, WithDataVolume'dan ONCE: veri yolu o andaki imaj annotation'indan cozulur.
var postgres = builder.AddPostgres("postgres")
    .WithImage("pgvector/pgvector", "pg17")
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
var customerDb = postgres.AddDatabase("customerDb");

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
    .WithReference(redis)
    .WaitFor(stockDb)
    .WaitFor(rabbit)
    .WaitFor(redis);

// 012: Basket & Order, Stock'a senkron gRPC (rezervasyon Reserve/Release/Commit) çağırır.
var basketApi = builder.AddProject<Projects.Basket_Api>("basket-api")
    .WithReference(basketDb)
    .WithReference(rabbit)
    .WithReference(stockApi)
    .WithReference(redis)
    .WaitFor(basketDb)
    .WaitFor(rabbit)
    .WaitFor(stockApi)
    .WaitFor(redis);

var orderApi = builder.AddProject<Projects.Order_Api>("order-api")
    .WithReference(orderDb)
    .WithReference(rabbit)
    .WithReference(stockApi)
    // 028: checkout saga ClearBasket adimi Basket gRPC ucunu cagirir.
    .WithReference(basketApi)
    .WithReference(redis)
    .WaitFor(orderDb)
    .WaitFor(rabbit)
    .WaitFor(stockApi)
    .WaitFor(basketApi)
    .WaitFor(redis);

var fileApi = builder.AddProject<Projects.File_Api>("file-api")
    .WithReference(fileDb)
    .WithReference(rabbit)
    .WithReference(redis)
    .WaitFor(fileDb)
    .WaitFor(rabbit)
    .WaitFor(redis);

var storefrontApi = builder.AddProject<Projects.Storefront_Api>("storefront-api")
    .WithReference(storefrontDb)
    .WithReference(rabbit)
    .WithReference(identityServer)
    .WithReference(redis)
    .WaitFor(storefrontDb)
    .WaitFor(rabbit)
    .WaitFor(identityServer)
    .WaitFor(redis);

var paymentApi = builder.AddProject<Projects.Payment_Api>("payment-api")
    .WithReference(paymentDb)
    .WithReference(redis)
    .WaitFor(paymentDb)
    .WaitFor(redis);

// 022: Customer BC — Wallet (kayitli kart) + AddressBook (adres defteri). Kendi DB'si;
// bu feature'da servisler-arasi event/gRPC yok (identity token'iyla korunan salt CRUD + MCP okuma).
var customerApi = builder.AddProject<Projects.Customer_Api>("customer-api")
    .WithReference(customerDb)
    .WithReference(identityServer)
    .WithReference(redis)
    .WaitFor(customerDb)
    .WaitFor(identityServer)
    .WaitFor(redis);

// 039: chat siparis tamamlama — Order.Api odeme baglamini (buyer+vaultToken+adres) Customer'dan
// yapisal REST ile ceker (customerApi orderApi'den SONRA tanimli oldugu icin referans burada eklenir).
orderApi.WithReference(customerApi).WaitFor(customerApi);

var gateway = builder.AddProject<Projects.Gateway>("gateway")
    .WithReference(catalogApi)
    .WithReference(basketApi)
    .WithReference(orderApi)
    .WithReference(paymentApi)
    .WithReference(stockApi)
    .WithReference(fileApi)
    .WithReference(storefrontApi)
    .WithReference(customerApi)
    .WithReference(identityServer)
    .WaitFor(identityServer);

var web = builder.AddProject<Projects.WebApp>("ecommerce-web");
web.WithReference(basketApi)
    .WithReference(stockApi)
    .WithReference(orderApi)
    .WithReference(fileApi)
    .WithReference(paymentApi)
    .WithReference(storefrontApi)
    .WithReference(customerApi)
    .WithReference(identityServer)
    .WaitFor(identityServer);


var chatAgent = builder.AddProject<Projects.ChatAgent>("chat-agent")
    .WithReference(gateway)
    // 024: uzak A2A PaymentAgent url'i (ayri solution). Bos/eksik ise ChatAgent taksit tool'unu
    // eklemeden acilir (graceful-degrade, US2). Uzak taraf gelince buraya adres verilir.
    .WithEnvironment("PaymentGateway__A2AUrl", builder.Configuration["PaymentGateway:A2AUrl"] ?? "")
    // 032: admin onboarding descriptor linki WebApp well-known'inden turetilir (service discovery).
    .WithReference(web)
    .WaitFor(gateway);

// Tedarikçi simülatörü: DB'siz mock — rev başına JSON dataset döner (041: iki tedarikçi + advance).
var supplierApi = builder.AddProject<Projects.Supplier_Api>("supplier-api");

// 041: Procurement BC — havuz + buy-box + enrich. WaitFor catalog/stock: tüketici kuyrukları
// publisher'dan önce bağlansın (soğuk açılışta binding'siz fanout = sessiz kayıp — 007/012 dersi).
var procurementDb = postgres.AddDatabase("procurementDb");
builder.AddProject<Projects.Procurement_Api>("procurement-api")
    .WithReference(procurementDb)
    .WithReference(rabbit)
    .WithReference(supplierApi)
    .WaitFor(procurementDb)
    .WaitFor(rabbit)
    .WaitFor(supplierApi)
    .WaitFor(catalogApi)
    .WaitFor(stockApi);

// WebApp chat widget'i orchestrator'a proxy uzerinden gider => adres cozumu icin referans.
web.WithReference(chatAgent);

await builder.Build().RunAsync();