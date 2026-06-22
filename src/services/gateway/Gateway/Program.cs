
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));


builder.Services.AddAuthenticationAndAuthorizationExtension(builder.Configuration);
var app = builder.Build();

app.MapDefaultEndpoints();
app.MapReverseProxy();
app.MapGet("/", () => "YARP (Gateway)");
app.UseAuthentication();
app.UseAuthorization();
await app.RunAsync();