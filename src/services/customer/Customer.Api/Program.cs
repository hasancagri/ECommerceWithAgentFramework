var builder = WebApplication.CreateBuilder(args);
builder.AddOpenApiDocumentation();

var customerDb = builder.Configuration.GetConnectionString("customerDb")!;
builder.Services.AddMarten(opts =>
    {
        opts.DatabaseSchemaName = SchemaConstants.CustomerSchemaName;
        opts.Connection(customerDb);
        opts.UseNewtonsoftForSerialization(
            nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
            configure: s => s.ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor);
        // 022: iki aggregate root, ikisi de UserId ile keyli (kullanici basina tek cuzdan/defter).
        opts.Schema.For<Customer.Api.Domains.Wallets.Wallet>().Index(x => x.UserId);
        opts.Schema.For<Customer.Api.Domains.AddressBooks.AddressBook>().Index(x => x.UserId);
        // Vault: DropShop merchant kimliği (tekil kayıt) — vault token'ı bundan mint edilir.
        opts.Schema.For<Customer.Api.Domains.MerchantInformations.MerchantInformation>();
    })
    .IntegrateWithWolverine()
    .ApplyAllDatabaseChangesOnStartup();

builder.Host.UseWolverine(opts =>
{
    // Dev: tek dugum (Solo) - leader election/node-agent koordinasyonu kapali; kirli kapanan
    // debug oturumlarinin hayalet-node StopRemoteAgent timeout gurultusunu kokten onler.
    if (builder.Environment.IsDevelopment())
        opts.Durability.Mode = DurabilityMode.Solo;

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
    AuthorizationScopes.CustomerRead,
    AuthorizationScopes.CustomerWrite,
    // Vault merchant kimliği yönetimi (admin-only capability).
    AuthorizationScopes.MerchantCredentialsWrite);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

// Vault: DropShop bağlantı config'i (section "DropShopVault") + gateway HTTP client.
builder.Services.AddOptionsExt();
// Dev: gateway self-signed sertifikasını kabul et (Aspire https). PROD'da kaldırılır.
builder.Services.AddHttpClient("dropshop-vault")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });

// L2 (paylaşımlı) önbellek katmanı — Redis IDistributedCache; opsiyonel (yoksa HybridCache yalnız L1).
if (builder.Configuration.GetConnectionString("redis") is not null)
    builder.AddRedisDistributedCache("redis");

// Declarative caching aspect'i: HybridCache + IMessageBus'ı şeffaf sar. UseWolverine'den sonra olmalı.
builder.Services.AddCachingAspect("customer");

builder.Services.AddHttpContextAccessor();
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();
app.MapScalarDocumentation();

var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

app.UseAuthentication();
app.UseAuthorization();

app.AddAddressBookGroupEndpointExtension(apiVersionSet);
app.AddWalletGroupEndpointExtension(apiVersionSet);
app.AddMerchantInformationGroupEndpointExtension(apiVersionSet);
// 039: Order.Api chat siparis tamamlama yapisal odeme-baglami ucu (customer.read makine token'i).
app.AddPaymentContextInternalEndpoint(apiVersionSet);

app.MapMcp("/mcp");

await app.RunAsync();