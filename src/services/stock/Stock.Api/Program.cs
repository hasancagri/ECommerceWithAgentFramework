
using Common.Utils.Authorization;
using Shared.Utils.Constants;

var builder = WebApplication.CreateBuilder(args);
builder.AddOpenApiDocumentation();

var stockDb = builder.Configuration.GetConnectionString("stockDb")!;
builder.Services.AddMarten(opts =>
    {
        opts.DatabaseSchemaName = SchemaConstants.StockSchemaName;
        opts.Connection(stockDb);
        opts.UseNewtonsoftForSerialization(
            nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
            configure: s => s.ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor);
        opts.Schema.For<ProductStock>().Index(x => x.ProductId);
    })
    .IntegrateWithWolverine()
    .ApplyAllDatabaseChangesOnStartup();

builder.Host.UseWolverine(opts =>
{
    var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
        .AutoProvision();

    rabbit.DeclareExchange(RabbitMqConstants.ProductCreated.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
        e.BindQueue(RabbitMqConstants.ProductCreated.Queues.Stock);
    });

    opts.ListenToRabbitQueue(RabbitMqConstants.ProductCreated.Queues.Stock);

    opts.Policies.UseDurableLocalQueues();
    // Rol yetkisi: middleware SADECE [RequiredRole] tasiyan komut/sorgulara weave edilir.
    opts.Policies.AddMiddleware(
        typeof(RoleAuthorizationMiddleware),
        chain => chain.MessageType.GetCustomAttribute<RequiredRoleAttribute>() is not null);
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
    AuthorizationScopes.StockRead,
    AuthorizationScopes.StockWrite);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();
// RoleAuthorizationMiddleware HttpContext'e erisir (token'daki role claim'i).
builder.Services.AddHttpContextAccessor();

var app = builder.Build();
app.MapScalarDocumentation();

var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

app.UseAuthentication();
app.UseAuthorization();

app.AddStockGroupEndpointExtension(apiVersionSet);

await app.RunAsync();