var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Cluster adresleri Aspire service discovery adlari (http://catalog-api gibi).
// AddServiceDiscoveryDestinationResolver: YARP bu adlari ServiceDefaults'in service
// discovery'si uzerinden (services__<ad>__http__0) gercek endpoint'e cozer.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();


builder.Services.AddAuthenticationAndAuthorizationExtension(builder.Configuration);

// YARP route'lari iki policy adi istiyor (appsettings.Development.json -> AuthorizationPolicy):
//   ClientCredential = uygulama token'i (catalog/file), Password = kullanici token'i (basket/order/...).
// Simdilik ikisi de yalnizca gecerli (authenticated) bir token sart kosuyor; grant-tipine gore
// ayristirma ertelenmis auth isine ait (bkz. dummy/deferred auth modeli).
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ClientCredential", policy => policy.RequireAuthenticatedUser());
    options.AddPolicy("Password", policy => policy.RequireAuthenticatedUser());
});

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapReverseProxy();
app.MapGet("/", () => "YARP (Gateway)");
app.UseAuthentication();
app.UseAuthorization();
await app.RunAsync();