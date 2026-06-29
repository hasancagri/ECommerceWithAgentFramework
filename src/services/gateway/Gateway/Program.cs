
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Cluster adresleri Aspire service discovery adlari (http://catalog-api gibi).
// AddServiceDiscoveryDestinationResolver: YARP bu adlari ServiceDefaults'in service
// discovery'si uzerinden (services__<ad>__http__0) gercek endpoint'e cozer.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();


builder.Services.AddAuthenticationAndAuthorizationExtension(builder.Configuration);
var app = builder.Build();

app.MapDefaultEndpoints();
app.MapReverseProxy();
app.MapGet("/", () => "YARP (Gateway)");
app.UseAuthentication();
app.UseAuthorization();
await app.RunAsync();