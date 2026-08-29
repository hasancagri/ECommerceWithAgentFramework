using Microsoft.Extensions.Options;
using Wolverine.RabbitMQ;
using static Shared.CheckoutMessages;

var builder = WebApplication.CreateBuilder(args);
builder.AddOpenApiDocumentation();

var checkoutDb = builder.Configuration.GetConnectionString("checkoutDb")!;
builder.Services.AddMarten(opts =>
    {
        opts.DatabaseSchemaName = SchemaConstants.CheckoutSchemaName;
        opts.Connection(checkoutDb);
        opts.UseNewtonsoftForSerialization(
            nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
            configure: s =>
            {
                s.ConstructorHandling = Newtonsoft.Json.ConstructorHandling.AllowNonPublicDefaultConstructor;
            });
    })
    .IntegrateWithWolverine()
    .ApplyAllDatabaseChangesOnStartup();

builder.Host.UseWolverine(opts =>
{
    if (builder.Environment.IsDevelopment())
        opts.Durability.Mode = DurabilityMode.Solo;

    var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
        .AutoProvision();

    // 049: hedefli komutlar per-BC kuyruğa; yanıtlar tek reply kuyruğundan dinlenir (broker saga).
    opts.PublishMessage<CreateOrderCommand>().ToRabbitQueue(RabbitMqConstants.Checkout.OrderCommandsQueue);
    opts.PublishMessage<ConfirmOrderCommand>().ToRabbitQueue(RabbitMqConstants.Checkout.OrderCommandsQueue);
    opts.PublishMessage<CancelOrderCommand>().ToRabbitQueue(RabbitMqConstants.Checkout.OrderCommandsQueue);
    opts.PublishMessage<ChargePaymentCommand>().ToRabbitQueue(RabbitMqConstants.Checkout.PaymentCommandsQueue);
    opts.PublishMessage<CommitStockCommand>().ToRabbitQueue(RabbitMqConstants.Checkout.StockCommandsQueue);
    opts.PublishMessage<RevertCommitStockCommand>().ToRabbitQueue(RabbitMqConstants.Checkout.StockCommandsQueue);
    opts.PublishMessage<ClearBasketCommand>().ToRabbitQueue(RabbitMqConstants.Checkout.BasketCommandsQueue);

    // Giriş: StartCheckout (WebApp endpoint local publish + chat/Order cross-service) buraya gelir → saga doğar.
    opts.PublishMessage<StartCheckout>().ToRabbitQueue(RabbitMqConstants.Checkout.StartQueue);
    opts.ListenToRabbitQueue(RabbitMqConstants.Checkout.StartQueue);

    // Hedef BC'ler yanıtları buraya yayınlar (tüketici binding'i burada — 007 dersi).
    opts.ListenToRabbitQueue(RabbitMqConstants.Checkout.RepliesQueue);

    // 049: geçici hata retry saga'da DEĞİL — Wolverine policy'de (FR-024). Artan gecikmeyle 3 deneme;
    // tükenirse mesaj dead-letter'a. (Aynı politika hedef BC'lerde de step-komut tüketiminde geçerli.)
    opts.OnException<Exception>().RetryWithCooldown(
        TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15));

    opts.Policies.UseDurableLocalQueues();
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
    AuthorizationScopes.CheckoutWrite);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

builder.Services.AddOptions<CheckoutOptions>().BindConfiguration(nameof(CheckoutOptions))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<CheckoutOptions>(sp => sp.GetRequiredService<IOptions<CheckoutOptions>>().Value);

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

app.AddCheckoutEndpoints(apiVersionSet);

app.MapMcp("/mcp");

await app.RunAsync();