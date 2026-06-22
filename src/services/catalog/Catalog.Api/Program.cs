
var builder = WebApplication.CreateBuilder(args);

var catalogDb = builder.Configuration.GetConnectionString("catalogDb")!;
builder.Services.AddMarten(opts =>
    {
        opts.DatabaseSchemaName = SchemaConstants.CATALOG_SCHEMA_NAME;
        opts.Connection(catalogDb);
        opts.UseNewtonsoftForSerialization(
            nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
            configure: s =>
            {
                s.ConstructorHandling = Newtonsoft.Json.ConstructorHandling.AllowNonPublicDefaultConstructor;
            });

        opts.Schema.For<Course>()
            .Index(x => x.UserId)
            .Index(x => x.CategoryId);

        opts.Schema.For<Category>();
    })
    .IntegrateWithWolverine()
    .ApplyAllDatabaseChangesOnStartup()
    .InitializeWith<SeedData>();


builder.Host.UseWolverine(opts =>
{
    var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
        .AutoProvision();

    rabbit.DeclareExchange(RabbitMqConstants.UploadCoursePicture.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
    });

    rabbit.DeclareExchange(RabbitMqConstants.CoursePictureUploaded.Exchange, e =>
    {
        e.ExchangeType = ExchangeType.Fanout;
        e.BindQueue(RabbitMqConstants.CoursePictureUploaded.Queues.Catalog);
    });

    opts.PublishMessage<Shared.IntegrationEvents.UploadCoursePictureCommand>()
        .ToRabbitExchange(RabbitMqConstants.UploadCoursePicture.Exchange);

    opts.ListenToRabbitQueue(RabbitMqConstants.CoursePictureUploaded.Queues.Catalog);

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

builder.Services.AddAuthenticationAndAuthorizationExtension(builder.Configuration);
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();


var app = builder.Build();

var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

app.UseAuthentication();
app.UseAuthorization();

app.AddCourseGroupEndpointExtension(apiVersionSet);
app.AddCategoryGroupEndpointExtension(apiVersionSet);

await app.RunAsync();