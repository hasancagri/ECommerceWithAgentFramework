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
var ucpDb = postgres.AddDatabase("ucpDb");


var identityServer = builder.AddProject<Projects.Identity_Server>("identity-server")
    .WithReference(identityDb)
    .WaitFor(identityDb);

var catalogApi = builder.AddProject<Projects.Catalog_Api>("catalog-api")
    .WithHttpHealthCheck("/health")
    .WithReference(catalogDb)
    .WithReference(rabbit)
    .WithReference(redis)
    .WaitFor(catalogDb)
    .WaitFor(rabbit)
    .WaitFor(redis);

var stockApi = builder.AddProject<Projects.Stock_Api>("stock-api")
    .WithHttpHealthCheck("/health")
    .WithReference(stockDb)
    .WithReference(rabbit)
    .WithReference(redis)
    .WaitFor(stockDb)
    .WaitFor(rabbit)
    .WaitFor(redis);

// 012: Basket & Order, Stock'a senkron gRPC (rezervasyon Reserve/Release/Commit) çağırır.
var basketApi = builder.AddProject<Projects.Basket_Api>("basket-api")
    .WithHttpHealthCheck("/health")
    .WithReference(basketDb)
    .WithReference(rabbit)
    .WithReference(stockApi)
    .WithReference(redis)
    .WaitFor(basketDb)
    .WaitFor(rabbit)
    .WaitFor(stockApi)
    .WaitFor(redis);

var orderApi = builder.AddProject<Projects.Order_Api>("order-api")
    .WithHttpHealthCheck("/health")
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
    .WithHttpHealthCheck("/health")
    .WithReference(storefrontDb)
    .WithReference(rabbit)
    .WithReference(identityServer)
    .WithReference(redis)
    .WaitFor(storefrontDb)
    .WaitFor(rabbit)
    .WaitFor(identityServer)
    .WaitFor(redis);

var paymentApi = builder.AddProject<Projects.Payment_Api>("payment-api")
    .WithHttpHealthCheck("/health")
    .WithReference(paymentDb)
    .WithReference(rabbit)
    .WithReference(redis)
    .WaitFor(paymentDb)
    .WaitFor(rabbit)
    .WaitFor(redis);

// 022: Customer BC — Wallet (kayitli kart) + AddressBook (adres defteri). Kendi DB'si;
// bu feature'da servisler-arasi event/gRPC yok (identity token'iyla korunan salt CRUD + MCP okuma).
var customerApi = builder.AddProject<Projects.Customer_Api>("customer-api")
    .WithHttpHealthCheck("/health")
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

// 060: Library BC — fiyat alarmı (yaşayan abonelik) + bildirim izi. Catalog'un product.changed
// fanout'unu dinler, alarm başına PriceAlarmTriggered yayınlar, NotificationSent izini yazar.
var libraryDb = postgres.AddDatabase("libraryDb");
var libraryApi = builder.AddProject<Projects.Library_Api>("library-api")
    .WithReference(libraryDb)
    .WithReference(rabbit)
    .WithReference(identityServer)
    .WaitFor(libraryDb)
    .WaitFor(rabbit)
    .WaitFor(identityServer);

// 072: UCP checkout kanalı BC (ucpDb). Kendi izole modeli; katalog projeksiyonu product/stock
// fanout'undan beslenir; session complete Order'a sanksiyonlu gRPC (already-captured). Order + Identity
// bekler (gRPC hedefi + token). Tüketici kuyruğu yayıncıdan önce ayakta olsun diye catalog/stock'u bekler.
var ucpApi = builder.AddProject<Projects.Ucp_Api>("ucp-api")
    .WithHttpHealthCheck("/health")
    .WithReference(ucpDb)
    .WithReference(rabbit)
    .WithReference(redis)
    .WithReference(orderApi)
    .WithReference(identityServer)
    .WaitFor(ucpDb)
    .WaitFor(rabbit)
    .WaitFor(redis)
    .WaitFor(orderApi)
    .WaitFor(identityServer)
    .WaitFor(catalogApi)
    .WaitFor(stockApi);

// 072: UCP platform simülatörü — DB'siz MCP server (Claude Desktop dış platform rolüyle bağlanır).
// Mağaza /ucp cephesini client_credentials (ucp-platform) ile sürer; Identity + ucp-api bekler.
builder.AddProject<Projects.Ucp_Sim>("ucp-sim")
    .WithReference(ucpApi)
    .WithReference(identityServer)
    .WaitFor(ucpApi)
    .WaitFor(identityServer);

// 060: Mailpit — dev posta kutusu (ham container; SMTP 1025 + web UI 8025).
var mailpit = builder.AddContainer("mailpit", "axllent/mailpit")
    .WithHttpEndpoint(targetPort: 8025, name: "http")
    .WithEndpoint(targetPort: 1025, name: "smtp")
    .WithLifetime(ContainerLifetime.Persistent);
var mailpitSmtp = mailpit.GetEndpoint("smtp");

// 060: Mail.Mcp — ilk standalone MCP server (send_mail). SMTP hedefi Mailpit endpoint'inden
// env ile (Options pattern SmtpOptions; IConfiguration'dan doğrudan okuma yok).
var mailMcp = builder.AddProject<Projects.Mail_Mcp>("mail-mcp")
    .WithEnvironment(ctx =>
    {
        ctx.EnvironmentVariables["Smtp__Host"] = mailpitSmtp.Property(EndpointProperty.Host);
        ctx.EnvironmentVariables["Smtp__Port"] = mailpitSmtp.Property(EndpointProperty.Port);
    })
    .WaitFor(mailpit);

// 060: NotificationAgent — DB'siz worker (Reviews.Moderation emsali); PriceAlarmTriggered tüketir,
// maili kişiselleştirip Mail.Mcp üzerinden gönderir. WebApp base-url env'i aşağıda (web tanımlanınca).
var notificationAgent = builder.AddProject<Projects.NotificationAgent>("notification-agent")
    .WithReference(rabbit)
    .WithReference(mailMcp)
    .WaitFor(rabbit)
    .WaitFor(mailMcp);

// 073: tek müşteri MCP fasadı (DB'siz) — alt BC /mcp'lerini toplayıp tek /mcp + /mcp-admin sunar.
// Downstream'lere service discovery için referans; lazy keşif olduğundan WaitFor kozmetik (correctness
// garanti). Identity token (discovery) + gateway route (aşağıda) ile tek dış giriş.
var mcpGateway = builder.AddProject<Projects.Mcp_Gateway>("mcp-gateway")
    .WithHttpHealthCheck("/health")
    .WithReference(storefrontApi)
    .WithReference(catalogApi)
    .WithReference(basketApi)
    .WithReference(orderApi)
    .WithReference(customerApi)
    .WithReference(paymentApi)
    .WithReference(stockApi)
    .WithReference(identityServer)
    .WaitFor(identityServer);

var gateway = builder.AddProject<Projects.Gateway>("gateway")
    .WithReference(catalogApi)
    .WithReference(basketApi)
    .WithReference(orderApi)
    .WithReference(paymentApi)
    .WithReference(stockApi)
    .WithReference(storefrontApi)
    .WithReference(customerApi)
    .WithReference(reviewsApi)
    // 065: Library MCP gateway üzerinden (dış agent fiyat alarmı); service discovery için referans.
    .WithReference(libraryApi)
    // 072: UCP checkout kanalı gateway üzerinden (/ucp + /.well-known/ucp); service discovery referansı.
    .WithReference(ucpApi)
    // 073: tek müşteri MCP fasadı (/mcp + /mcp-admin) gateway üzerinden.
    .WithReference(mcpGateway)
    .WithReference(identityServer)
    .WaitFor(identityServer);

var web = builder.AddProject<Projects.WebApp>("ecommerce-web");
web.WithReference(basketApi)
    // 058: admin ürün düzenleme ekranları Catalog'un yönetim penceresini çağırır.
    .WithReference(catalogApi)
    .WithReference(stockApi)
    .WithReference(orderApi)
    .WithReference(paymentApi)
    .WithReference(storefrontApi)
    .WithReference(customerApi)
    .WithReference(reviewsApi)
    // 060: detay sayfası fiyat alarmı düğmesi Library.Api'ye Refit ile gider (gateway route yok).
    .WithReference(libraryApi)
    .WithReference(identityServer)
    .WaitFor(identityServer);


var chatAgent = builder.AddProject<Projects.ChatAgent>("chat-agent")
    .WithReference(gateway)
    // 024: uzak A2A PaymentAgent url'i (ayri solution). Bos/eksik ise ChatAgent taksit tool'unu
    // eklemeden acilir (graceful-degrade, US2). Uzak taraf gelince buraya adres verilir.
    .WithEnvironment("PaymentGateway__A2AUrl", builder.Configuration["PaymentGateway:A2AUrl"] ?? "")
    // 032: admin onboarding descriptor linki WebApp well-known'inden turetilir (service discovery).
    .WithReference(web)
    // Keşif makine token'ı (chat-agent-discovery client_credentials) Identity'den alınır.
    .WithReference(identityServer)
    .WaitFor(identityServer)
    .WaitFor(gateway)
    // 069 canli bulgu: MAF agent'lari STARTUP'ta kurulur (Map* cagrisi resolve eder) ve MCP tool'lari
    // o anda toplanir — tool MCP'si ayakta degilse agent KALICI tool'suz kalir (singleton, retry yok).
    // Bu yuzden chat-agent tool topladigi TUM ic MCP servislerini bekler.
    .WaitFor(storefrontApi)
    .WaitFor(catalogApi)
    .WaitFor(basketApi)
    .WaitFor(orderApi)
    .WaitFor(paymentApi)
    .WaitFor(stockApi)
    .WaitFor(customerApi);

// WebApp chat widget'i orchestrator'a proxy uzerinden gider => adres cozumu icin referans.
web.WithReference(chatAgent);

// 060: mail'deki urun linki MUTLAK WebApp adresiyle kurulur (relatif link Mailpit UI'da 404).
notificationAgent.WithEnvironment("WebApp__BaseUrl", web.GetEndpoint("https"));

// 049: WebApp checkout girişi Checkout.Orchestrator'a POST eder → adres çözümü için referans.
web.WithReference(checkoutOrchestrator);

await builder.Build().RunAsync();