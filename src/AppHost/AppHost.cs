var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

var redis = builder.AddRedis("redis")
    .WithRedisInsight()
    .WithLifetime(ContainerLifetime.Persistent);

var rabbit = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin()
    .WithLifetime(ContainerLifetime.Persistent);

var catalogDb = postgres.AddDatabase("catalogDb");
var basketDb = postgres.AddDatabase("basketDb");
var orderDb = postgres.AddDatabase("orderDb");
var discountDb = postgres.AddDatabase("discountDb");
var fileDb = postgres.AddDatabase("fileDb");
var paymentDb = postgres.AddDatabase("paymentDb");
var stockDb = postgres.AddDatabase("stockDb");
var identityDb = postgres.AddDatabase("identityDb");

var identityServer = builder.AddProject<Projects.Identity_Server>("identity-server")
    .WithReference(identityDb)
    .WaitFor(identityDb);

var catalogApi = builder.AddProject<Projects.Catalog_Api>("catalog-api")
    .WithReference(catalogDb)
    .WithReference(redis)
    .WithReference(rabbit)
    .WaitFor(catalogDb)
    .WaitFor(redis)
    .WaitFor(rabbit);

var stockApi = builder.AddProject<Projects.Stock_Api>("stock-api")
    .WithReference(stockDb)
    .WithReference(redis)
    .WithReference(rabbit)
    .WaitFor(stockDb)
    .WaitFor(redis)
    .WaitFor(rabbit);

var basketApi = builder.AddProject<Projects.Basket_Api>("basket-api")
    .WithReference(basketDb)
    .WithReference(redis)
    .WithReference(rabbit)
    .WaitFor(basketDb)
    .WaitFor(redis)
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

var paymentApi = builder.AddProject<Projects.Payment_Api>("payment-api")
    .WithReference(paymentDb)
    .WaitFor(paymentDb);

// Gateway (YARP): downstream'lere service discovery ile baglanir (cluster adresleri
// http://catalog-api gibi). Proxy'ledigi servisleri ve auth icin identity'yi referans alir.
var gateway = builder.AddProject<Projects.Gateway>("gateway")
    .WithReference(catalogApi)
    .WithReference(basketApi)
    .WithReference(discountApi)
    .WithReference(orderApi)
    .WithReference(paymentApi)
    .WithReference(fileApi)
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
    .WithReference(identityServer)
    .WaitFor(identityServer);

// AgentOrchestrator: MCP tool'lari uzerinden calisan AI agent API'si (OpenAI uyumlu endpoint'ler).
// MCP server'lara Gateway uzerinden baglanir; gateway'i WithReference ile alir =>
// services:gateway:http:0 cozulur (fallback'e gerek kalmaz). OpenAI:ApiKey kendi user-secrets'inden.
builder.AddProject<Projects.AgentOrchestrator>("agent-orchestrator")
    .WithReference(gateway)
    .WaitFor(gateway);


await builder.Build().RunAsync();