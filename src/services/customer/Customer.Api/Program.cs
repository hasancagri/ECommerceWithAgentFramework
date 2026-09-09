var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
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

        // 070: admin yazma tool'larının salt-append denetim izi (FR-009).
        opts.Schema.For<Customer.Api.AdminAudit.AdminActionLog>();
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
// 061: RFC 9728 keşif (metadata dokümanı + 401 challenge parametreleri) — dış agent OAuth zinciri.
// 070 fix: 062 adres YAZMA tool'ları açıldığında bu liste bayat kalmıştı — scope'unu PRM'den türeten
// istemciler (mcp-remote köprüsü) customer.write'sız token alıp add_address'te düşüyordu.
builder.Services.AddMcpResourceMetadata(builder.Configuration, "customer",
    AuthorizationScopes.CustomerRead, AuthorizationScopes.CustomerWrite);
// 061 logout: `logout` MCP tool'unun Identity.Server agent-logout ucuna forward client'ı.
builder.Services.AddAgentLogoutClient(builder.Configuration);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

// Vault: DropShop bağlantı config'i (section "DropShopVault") + gateway HTTP client.
builder.Services.AddOptionsExt();

// 070 FR-016: DropShop onboarding sarmalayıcısı — PG Merchant.Api MCP'sine makine kimliği
// (client_credentials) forward eden named-client (MCP uzun-ömürlü SSE → resilience muaf).
builder.Services.AddTransient<Customer.Api.Onboarding.OnboardingGatewayTokenHandler>();
#pragma warning disable EXTEXP0001 // RemoveAllResilienceHandlers experimental; MCP SSE icin gerekli
builder.Services.AddHttpClient(Customer.Api.Onboarding.MerchantOnboardingClient.HttpClientName)
    .RemoveAllResilienceHandlers()
    .AddHttpMessageHandler<Customer.Api.Onboarding.OnboardingGatewayTokenHandler>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });
// Dev: gateway self-signed sertifikasını kabul et (Aspire https). PROD'da kaldırılır.
builder.Services.AddHttpClient("dropshop-vault")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });
#pragma warning restore EXTEXP0001

// L2 (paylaşımlı) önbellek katmanı — Redis IDistributedCache; opsiyonel (yoksa HybridCache yalnız L1).
if (builder.Configuration.GetConnectionString("redis") is not null)
    builder.AddRedisDistributedCache("redis");

// Declarative caching aspect'i: HybridCache + IMessageBus'ı şeffaf sar. UseWolverine'den sonra olmalı.
builder.Services.AddCachingAspect("customer");

builder.Services.AddHttpContextAccessor();
// 070: TEK MCP server, İKİ korumalı uç — /mcp (müşteri tool seti, 061) + /mcp-admin (merchant
// yönetimi). Oturum başına TAZE options; tool seti isteğin yoluna göre budanır: admin tool'lar
// YALNIZ /mcp-admin'de, müşteri tool'ları YALNIZ /mcp'de görünür (müşteri DCR istemcileri admin
// şemasını görmez — R1).
string[] customerAdminToolNames =
[
    Shared.CustomerAdminTools.GetMerchantStatus, Shared.CustomerAdminTools.SetMerchantCredentials,
    Shared.CustomerAdminTools.SubmitOnboarding, Shared.CustomerAdminTools.OnboardingStatus,
];
builder.Services
    .AddMcpServer()
    .WithHttpTransport(http => http.ConfigureSessionOptions = (ctx, opts, _) =>
    {
        var isAdmin = ctx.Request.Path.StartsWithSegments("/mcp-admin");
        var tools = opts.ToolCollection;
        if (tools is null)
            return Task.CompletedTask;
        foreach (var tool in tools
                     .Where(t => customerAdminToolNames.Contains(t.ProtocolTool.Name) != isAdmin).ToArray())
            tools.Remove(tool);
        return Task.CompletedTask;
    })
    .WithToolsFromAssembly();

// 070: /mcp-admin RFC 9728 keşfi — admin scope'uyla (challenge yol-prefix'ine göre seçilir).
builder.Services.AddMcpAdminResourceMetadata(builder.Configuration, "customer",
    AuthorizationScopes.MerchantCredentialsWrite);

var app = builder.Build();
// AppHost WithHttpHealthCheck("/health") bu ucu yoklar (Development-only map).
app.MapDefaultEndpoints();
app.MapScalarDocumentation();

var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

app.UseAuthentication();
app.UseAuthorization();

app.AddMerchantInformationGroupEndpointExtension(apiVersionSet);
// 039: Order.Api chat siparis tamamlama yapisal odeme-baglami ucu (customer.read makine token'i).
app.AddPaymentContextInternalEndpoint(apiVersionSet);
// 049: Order.Api charge/reconcile merchant API key ucu (customer.read); MerchantKey agent'a cikmaz.
app.AddMerchantKeyInternalEndpoint(apiVersionSet);

// 061: MCP korumalı — kimliksiz istek 401 + resource_metadata challenge alır (dış agent keşfi).
app.MapMcp("/mcp").RequireAuthorization();
// 070: yönetim ucu — merchant admin tool'ları yalnız burada (scope katmanı handler'larda).
app.MapMcp("/mcp-admin").RequireAuthorization();
app.MapMcpResourceMetadata();

await app.RunAsync();