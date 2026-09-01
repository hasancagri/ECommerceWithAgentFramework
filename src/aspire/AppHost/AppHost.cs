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
var paymentDb = postgres.AddDatabase("paymentDb");
var stockDb = postgres.AddDatabase("stockDb");
var identityDb = postgres.AddDatabase("identityDb");
var storefrontDb = postgres.AddDatabase("storefrontDb");
var customerDb = postgres.AddDatabase("customerDb");
var checkoutDb = postgres.AddDatabase("checkoutDb");

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
    .WithReference(rabbit)
    .WithReference(redis)
    .WaitFor(paymentDb)
    .WaitFor(rabbit)
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

// 049: Checkout.Orchestrator — ayrı BC (checkoutDb), broker-only saga. Komutları hedef BC'lere
// yayınlar, yanıtları reply kuyruğundan dinler. BC komut-kuyruğu tüketicileri önce ayağa kalksın
// (soğuk-açılış binding dersi, 007). Giriş endpoint'i checkout.write ile korunur (identity).
var checkoutOrchestrator = builder.AddProject<Projects.Checkout_Orchestrator>("checkout-orchestrator")
    .WithReference(checkoutDb)
    .WithReference(rabbit)
    .WithReference(identityServer)
    .WaitFor(checkoutDb)
    .WaitFor(rabbit)
    .WaitFor(orderApi)
    .WaitFor(stockApi)
    .WaitFor(paymentApi)
    .WaitFor(basketApi);

// 044: Reviews BC — satin-alma sartli yorum + puan ozeti. Satin-alma kaniti icin Order gRPC'sine
// senkron sorar (fail-closed); ozet ReviewSummaryChanged fanout'uyla Storefront'a akar.
var reviewsDb = postgres.AddDatabase("reviewsDb");
var reviewsApi = builder.AddProject<Projects.Reviews_Api>("reviews-api")
    .WithReference(reviewsDb)
    .WithReference(rabbit)
    .WithReference(orderApi)
    .WaitFor(reviewsDb)
    .WaitFor(rabbit)
    .WaitFor(orderApi)
    // Tuketici kuyrugu yayincidan once baglansin (007 dersi): Storefront reviews'tan once ayakta.
    .WaitFor(storefrontApi);

// 046: Reviews moderasyon worker'i — DB'siz agent process (ChatAgent emsali). Reviews ile yalniz
// RabbitMQ event'leriyle konusur (ReviewModerationRequested tuket → ReviewModerated yayinla).
// OpenAI user-secret bu projede; Reviews'in OpenAI bagimliligi kalkti.
builder.AddProject<Projects.Reviews_Moderation>("reviews-moderation-agent")
    .WithReference(rabbit)
    .WaitFor(rabbit);

var gateway = builder.AddProject<Projects.Gateway>("gateway")
    .WithReference(catalogApi)
    .WithReference(basketApi)
    .WithReference(orderApi)
    .WithReference(paymentApi)
    .WithReference(stockApi)
    .WithReference(storefrontApi)
    .WithReference(customerApi)
    .WithReference(reviewsApi)
    .WithReference(identityServer)
    .WaitFor(identityServer);

var web = builder.AddProject<Projects.WebApp>("ecommerce-web");
web.WithReference(basketApi)
    .WithReference(stockApi)
    .WithReference(orderApi)
    .WithReference(paymentApi)
    .WithReference(storefrontApi)
    .WithReference(customerApi)
    .WithReference(reviewsApi)
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

// WebApp chat widget'i orchestrator'a proxy uzerinden gider => adres cozumu icin referans.
web.WithReference(chatAgent);

// 053: RecoTrainer — Python kişiselleştirme beyni (048 Personalization.Api emekli). Aspire 13
// AddUvicornApp (resmi Aspire.Hosting.Python) uvicorn ile host eder; kendi Postgres feature store'u.
// Gezinme sinyali = WebApp HTTP POST (m2m); satın-alma = Storefront 'PurchaseEnriched' broker event
// (Python tüketir, binding'i tüketici kurar). Tüketici PurchaseEnriched yayıncısından (storefront)
// sonra ayakta olması sorun değil — binding'i Python kurar (007 dersi tersi: tüketici-kurar).
var recoTrainerDb = postgres.AddDatabase("recoTrainerDb");
var recoTrainer = builder.AddUvicornApp("reco-trainer", "../../services/RecoTrainer", "reco_trainer.app:app")
    .WithUv()
    .WithReference(recoTrainerDb)
    .WithReference(rabbit)
    .WaitFor(recoTrainerDb)
    .WaitFor(rabbit);

// WebApp gezinme sinyallerini + profil okumasını reco-trainer'a gönderir → adres çözümü için referans.
web.WithReference(recoTrainer);

// 049: WebApp checkout girişi Checkout.Orchestrator'a POST eder → adres çözümü için referans.
web.WithReference(checkoutOrchestrator);

await builder.Build().RunAsync();