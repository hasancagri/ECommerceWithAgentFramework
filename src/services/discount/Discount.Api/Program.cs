
using Common.Utils.Authorization;
using Shared.Utils.Constants;

var builder = WebApplication.CreateBuilder(args);
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

        opts.Schema.For<Discount.Api.Domains.Discounts.Discount>();
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
        e.BindQueue(RabbitMqConstants.OrderCreated.Queues.Discount);
    });

    opts.ListenToRabbitQueue(RabbitMqConstants.OrderCreated.Queues.Discount);

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
    AuthorizationScopes.DiscountRead,
    AuthorizationScopes.DiscountWrite);
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

app.AddDiscountGroupEndpointExtension(apiVersionSet);

await app.RunAsync();