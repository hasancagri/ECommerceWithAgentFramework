var builder = WebApplication.CreateBuilder(args);
builder.AddOpenApiDocumentation();

var basketDb = builder.Configuration.GetConnectionString("basketDb")!;
builder.Services.AddMarten(opts =>
    {
        opts.DatabaseSchemaName = SchemaConstants.BasketSchemaName;
        opts.Connection(basketDb);
        opts.UseNewtonsoftForSerialization(
            nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
            configure: s => s.ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor);
        opts.Schema.For<Basket.Api.Domains.Baskets.Basket>().Index(x => x.UserId);
    })
    .IntegrateWithWolverine()
    .ApplyAllDatabaseChangesOnStartup();

builder.Host.UseWolverine(opts =>
{
    var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
        .AutoProvision();

    rabbit.DeclareExchange(RabbitMqConstants.OrderCreated.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
        e.BindQueue(RabbitMqConstants.OrderCreated.Queues.Basket);
    });

    opts.ListenToRabbitQueue(RabbitMqConstants.OrderCreated.Queues.Basket);

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
    AuthorizationScopes.BasketRead,
    AuthorizationScopes.BasketWrite);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();

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

app.AddBasketGroupEndpointExtension(apiVersionSet);

app.MapMcp("/mcp");

await app.RunAsync();