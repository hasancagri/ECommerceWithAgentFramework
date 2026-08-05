var builder = WebApplication.CreateBuilder(args);
builder.AddOpenApiDocumentation();

var orderDb = builder.Configuration.GetConnectionString("orderDb")!;
builder.Services.AddMarten(opts =>
    {
        opts.DatabaseSchemaName = SchemaConstants.OrderSchemaName;
        opts.Connection(orderDb);
        opts.UseNewtonsoftForSerialization(
            nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
            configure: s =>
            {
                s.ConstructorHandling = Newtonsoft.Json.ConstructorHandling.AllowNonPublicDefaultConstructor;
            });

        opts.Schema.For<Order.Api.Domains.Orders.Order>().Index(x => x.BuyerId);
    })
    .IntegrateWithWolverine()
    .ApplyAllDatabaseChangesOnStartup();

builder.Host.UseWolverine(opts =>
{
    // Dev: tek dugum (Solo) - leader election/node-agent koordinasyonu kapali; kirli kapanan
    // debug oturumlarinin hayalet-node StopRemoteAgent timeout gurultusunu kokten onler.
    if (builder.Environment.IsDevelopment())
        opts.Durability.Mode = DurabilityMode.Solo;

    // 012: gRPC tipli client (AddGrpcClient) opaque factory'dir; Wolverine handler codegen'i inline
    // kuramaz ve service-location ister. StockCommitClientProxy CreateOrder handler'ina enjekte edilir.
    opts.ServiceLocationPolicy = JasperFx.CodeGeneration.Model.ServiceLocationPolicy.AllowedButWarn;

    // 028: OrderCreated exchange kaldirildi; sepet temizligi CheckoutSaga gRPC adimi.
    opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
        .AutoProvision();

    opts.Policies.UseDurableLocalQueues();
    opts.Policies.AddMiddleware(
        typeof(Common.Utils.Authorization.ScopeAuthorizationMiddleware),
        chain => chain.MessageType.GetCustomAttribute<Common.Utils.Authorization.RequiredScopeAttribute>() is not null);
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
});


builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ReportApiVersions = true;
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

builder.Services.AddAuthenticationAndAuthorizationExtension(
    builder.Configuration,
    AuthorizationScopes.OrderRead,
    AuthorizationScopes.OrderWrite);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();
builder.Services.AddHttpContextAccessor();

// 028: saga adimlari arka planda kosar (HttpContext yok) — kullanici bearer'i yerine
// client-credentials makine token'i (order-saga) SagaTokenHandler ile eklenir.
builder.Services.AddTransient<SagaTokenHandler>();
// gRPC balancer'inin Aspire service-discovery cozumleyicisi YOK; 'stock-api' adini Aspire'in
// enjekte ettigi cozumlenmis endpoint'ten alip somut adresi veriyoruz.
var stockGrpcAddress = builder.Configuration["services:stock-api:https:0"]
    ?? builder.Configuration["services:stock-api:http:0"]
    ?? "https://stock-api";
builder.Services
    .AddGrpcClient<StockReservation.StockReservationClient>(o => o.Address = new Uri(stockGrpcAddress))
    .AddHttpMessageHandler<SagaTokenHandler>();
// Proxy'yi somut tipiyle kaydet: saga handler'lari onu concrete type ile ister.
builder.Services.AddScoped<StockCommitClientProxy>();

// 028: ClearBasket gRPC istemcisi (saga pivot-sonrasi adimi).
var basketGrpcAddress = builder.Configuration["services:basket-api:https:0"]
    ?? builder.Configuration["services:basket-api:http:0"]
    ?? "https://basket-api";
builder.Services
    .AddGrpcClient<Shared.Grpc.Basket.BasketClear.BasketClearClient>(o => o.Address = new Uri(basketGrpcAddress))
    .AddHttpMessageHandler<SagaTokenHandler>();
builder.Services.AddScoped<BasketClearClientProxy>();

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

// Dis tuketiciler icin opak UserKey (X-User-Key) custom auth semasi.
builder.Services.AddApiKeyAuthentication(builder.Configuration);

var app = builder.Build();
app.MapScalarDocumentation();

var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

app.UseAuthentication();
app.UseApiKeyAuthentication();
app.UseAuthorization();

app.AddOrderGroupEndpointExtension(apiVersionSet);

app.MapMcp("/mcp");

await app.RunAsync();