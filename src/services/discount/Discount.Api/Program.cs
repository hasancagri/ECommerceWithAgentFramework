using Discount.Api.Domains.ProductCatalogRefs;
using Discount.Api.Domains.ProductDiscounts;
using Discount.Api.Grpc;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddOpenApiDocumentation();

var discountDb = builder.Configuration.GetConnectionString("discountDb")!;
builder.Services.AddMarten(opts =>
    {
        opts.DatabaseSchemaName = SchemaConstants.DiscountSchemaName;
        opts.Connection(discountDb);
        opts.UseNewtonsoftForSerialization(
            nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
            configure: s =>
            {
                s.ConstructorHandling = Newtonsoft.Json.ConstructorHandling.AllowNonPublicDefaultConstructor;
            });

        // Sabit alias'lar savunma amaçlı (tr-TR dotless-ı tuzağı — 077 dersi; bu adlarda ı yok ama
        // güvenli taraf). ProductDiscount/ProductCatalogRef read-model'leri ProductId ile kimliklenir.
        opts.Schema.For<Campaign>().DocumentAlias("campaign").Index(x => x.Status);
        opts.Schema.For<ProductDiscount>().DocumentAlias("product_discount")
            .Identity(x => x.ProductId).Index(x => x.CampaignId);
        opts.Schema.For<ProductCatalogRef>().DocumentAlias("product_catalog_ref")
            .Identity(x => x.ProductId);
    })
    .IntegrateWithWolverine()
    .ApplyAllDatabaseChangesOnStartup();

builder.Host.UseWolverine(opts =>
{
    if (builder.Environment.IsDevelopment())
        opts.Durability.Mode = DurabilityMode.Solo;

    // Apply/Clear helper'ı typed hizmet enjekte etmez ama inline codegen güvenli tarafta kalsın.
    opts.ServiceLocationPolicy = JasperFx.CodeGeneration.Model.ServiceLocationPolicy.AllowedButWarn;

    var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!).AutoProvision();

    // 079: kitap başına indirim penceresi (fanout) → Storefront tüketir. Yayıncı yalnız exchange deklare eder.
    rabbit.DeclareExchange(RabbitMqConstants.ProductDiscountChanged.Exchange, e => e.ExchangeType = ExchangeType.Fanout);
    opts.PublishMessage<IntegrationEvents.ProductDiscountChanged>()
        .ToRabbitExchange(RabbitMqConstants.ProductDiscountChanged.Exchange);

    // 079: Catalog ProductChangedEvent TÜKETİLİR (ProductCatalogRef besleme) — binding'i tüketici kurar (007).
    rabbit.DeclareExchange(RabbitMqConstants.ProductChanged.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
        e.BindQueue(RabbitMqConstants.ProductChanged.Queues.Discount);
    });
    opts.ListenToRabbitQueue(RabbitMqConstants.ProductChanged.Queues.Discount).Sequential();

    // Süre yönetimi scheduled message (in-proc durable local queue).
    opts.Policies.UseDurableLocalQueues();
    opts.Policies.AddMiddleware(
        typeof(Common.Utils.Authorization.ScopeAuthorizationMiddleware),
        chain => chain.MessageType.GetCustomAttribute<Common.Utils.Authorization.RequiredScopeAttribute>() is not null);
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
    // "Consumers"/Process sınıfları taramada atlanabilir → açık kayıt garantili yol.
    opts.Discovery.IncludeType(typeof(Discount.Api.CatalogConsumers));
    opts.Discovery.IncludeType(typeof(Discount.Api.Process.CampaignScheduleHandler));
});

builder.Services.AddAuthenticationAndAuthorizationExtension(
    builder.Configuration,
    AuthorizationScopes.DiscountRead,
    AuthorizationScopes.AdminDiscountWrite);

// 070: /mcp-admin RFC 9728 keşfi (401 challenge + metadata) — admin scope'uyla. Anonim /mcp YOK.
builder.Services.AddMcpAdminResourceMetadata(builder.Configuration, "discount",
    AuthorizationScopes.AdminDiscountWrite);

builder.Services.AddOptions<IdentityOption>().BindConfiguration(nameof(IdentityOption))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<IdentityOption>(sp => sp.GetRequiredService<IOptions<IdentityOption>>().Value);

builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();
builder.Services.AddHttpContextAccessor();
builder.Services.AddGrpc();

// 070/074: tek MCP server, YALNIZ korumalı /mcp-admin ucu (kampanya = admin işi). Oturum başına taze
// options; tool seti yol-prefix'iyle budanır — allowlist YALNIZ /mcp-admin'de görünür (yeni tool → allowlist'e EKLE).
string[] discountAdminToolNames =
[
    Shared.DiscountAdminTools.CreateCampaign,
    Shared.DiscountAdminTools.CancelCampaign,
    Shared.DiscountAdminTools.ListCampaigns,
];
builder.Services
    .AddMcpServer()
    .WithHttpTransport(http => http.ConfigureSessionOptions = (ctx, mcpOptions, _) =>
    {
        var isAdmin = ctx.Request.Path.StartsWithSegments("/mcp-admin");
        var tools = mcpOptions.ToolCollection;
        if (tools is null)
            return Task.CompletedTask;
        foreach (var tool in tools
                     .Where(t => discountAdminToolNames.Contains(t.ProtocolTool.Name) != isAdmin).ToArray())
            tools.Remove(tool);
        return Task.CompletedTask;
    })
    .WithToolsFromAssembly();

var app = builder.Build();
app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseAuthorization();

// 079 US3: checkout S2S — Order.Api canlı indirim doğrulaması (discount.read).
app.MapGrpcService<DiscountQueryGrpcService>().RequireAuthorization(AuthorizationScopes.DiscountRead);

// 070: korumalı yönetim ucu — kimliksiz istek 401 + resource_metadata challenge.
app.MapMcp("/mcp-admin").RequireAuthorization();
app.MapMcpResourceMetadata();

await app.RunAsync();
