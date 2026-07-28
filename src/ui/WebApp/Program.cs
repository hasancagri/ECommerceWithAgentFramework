using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Localization;
using Refit;
using System.Globalization;
using WebApp.ExceptionHandlers;
using WebApp.Services;
using WebApp.Services.Refit;
using WebApp.Authentication;
using WebApp.Chat;
using WebApp.Extensions;


var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();


builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "keys")))
    .SetApplicationName("WebAppProtectionKeys").SetDefaultKeyLifetime(TimeSpan.FromDays(60));


// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddMvc(opt => opt.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);
builder.Services.AddOptionsExt();


// Identity.Server baglantisi icin tek config kaynagi.
var identitySettings = builder.Configuration.GetSection("IdentityServer").Get<IdentityServerSettings>()
                       ?? throw new InvalidOperationException("'IdentityServer' configuration section is missing.");
builder.Services.AddSingleton(identitySettings);

// TokenService'in M2M/refresh icin kullandigi adsiz-degil "identity" client'i.
builder.Services.AddHttpClient("identity");

// AI chat orchestrator (service discovery: services:chat-agent:http:0).
// Streaming oldugu icin uzun timeout.
builder.Services.AddHttpClient("orchestrator", client =>
{
    client.BaseAddress = new Uri("http://chat-agent");
    client.Timeout = TimeSpan.FromMinutes(5);
});

// Urun gorselleri icin file-api'ye BFF proxy'si (service discovery: services:file-api:http:0).
// DB'deki URL gateway-goreli (/file/images/{id}.png); WebApp ayni yolu ayni origin'den servis eder.
builder.Services.AddHttpClient("file-images", client =>
{
    client.BaseAddress = new Uri("http://file-api");
});

builder.Services.AddScoped<TokenService>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<BasketService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<StockService>();
builder.Services.AddScoped<StorefrontService>();

builder.Services.AddScoped<AuthenticatedHttpClientHandler>();
builder.Services.AddScoped<ClientAuthenticatedHttpClientHandler>();
builder.Services.AddExceptionHandler<UnauthorizedAccessExceptionHandler>();

builder.Services.AddRefitClient<ICatalogRefitService>().ConfigureHttpClient(configure =>
    {
        configure.BaseAddress = new Uri("http://catalog-api");
    }).AddHttpMessageHandler<AuthenticatedHttpClientHandler>()
    .AddHttpMessageHandler<ClientAuthenticatedHttpClientHandler>();


builder.Services.AddRefitClient<IBasketRefitService>().ConfigureHttpClient(configure =>
    {
        configure.BaseAddress = new Uri("http://basket-api");
    }).AddHttpMessageHandler<AuthenticatedHttpClientHandler>()
    .AddHttpMessageHandler<ClientAuthenticatedHttpClientHandler>();


builder.Services.AddRefitClient<IOrderRefitService>().ConfigureHttpClient(configure =>
    {
        configure.BaseAddress = new Uri("http://order-api");
    }).AddHttpMessageHandler<AuthenticatedHttpClientHandler>()
    .AddHttpMessageHandler<ClientAuthenticatedHttpClientHandler>();


builder.Services.AddRefitClient<IPaymentRefitService>().ConfigureHttpClient(configure =>
    {
        configure.BaseAddress = new Uri("http://payment-api");
    }).AddHttpMessageHandler<AuthenticatedHttpClientHandler>()
    .AddHttpMessageHandler<ClientAuthenticatedHttpClientHandler>();


builder.Services.AddRefitClient<IStockRefitService>().ConfigureHttpClient(configure =>
    {
        configure.BaseAddress = new Uri("http://stock-api");
    }).AddHttpMessageHandler<AuthenticatedHttpClientHandler>()
    .AddHttpMessageHandler<ClientAuthenticatedHttpClientHandler>();


// 006: ana sayfa vitrin listesi; okuma anonim, handler zinciri diğer istemcilerle tutarlılık için.
builder.Services.AddRefitClient<IStorefrontRefitService>().ConfigureHttpClient(configure =>
    {
        configure.BaseAddress = new Uri("http://storefront-api");
    }).AddHttpMessageHandler<AuthenticatedHttpClientHandler>()
    .AddHttpMessageHandler<ClientAuthenticatedHttpClientHandler>();


builder.Services.AddAuthentication(configureOption =>
    {
        configureOption.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        configureOption.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.ExpireTimeSpan = TimeSpan.FromDays(60);
        options.Cookie.Name = "WebAppCookie";
        options.AccessDeniedPath = "/Auth/AccessDenied";
    })
    .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
    {
        // Authorization Code (+ PKCE) akisi; login UI Identity.Server'da.
        options.Authority = identitySettings.Authority;
        options.ClientId = identitySettings.ClientId;
        options.ClientSecret = identitySettings.ClientSecret;
        options.ResponseType = "code";
        options.UsePkce = true;

        options.SaveTokens = true; // access/refresh token'lari cookie'de sakla
        options.GetClaimsFromUserInfoEndpoint = true;
        options.RequireHttpsMetadata = false; // dev (localhost) kolayligi

        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.Scope.Add("roles");
        options.Scope.Add("offline_access"); // refresh token
        // catalog (okuma anonim — read scope istenmez)
        options.Scope.Add("catalog.write");
        // basket
        options.Scope.Add("basket.read");
        options.Scope.Add("basket.write");
        // order
        options.Scope.Add("order.read");
        options.Scope.Add("order.write");
        // payment
        options.Scope.Add("payment.read");
        options.Scope.Add("payment.write");
        // 012: sepete ekleme/siparis Basket/Order -> Stock gRPC rezervasyonu icin.
        options.Scope.Add("stock.reserve");

        // Token'daki "name"/"role" claim'lerini standart tiplere esle (policy'ler icin).
        // role/email id_token'da geliyor (Config.cs: AlwaysIncludeUserClaimsInIdToken),
        // OIDC handler bunlari otomatik principal'a kopyalar.
        options.TokenValidationParameters = new()
        {
            NameClaimType = "name",
            RoleClaimType = "role",
        };

        // SignUp akisinda set edilen "prompt=create"i authorize istegine tasi
        // (Identity.Server bunu gorup kayit sayfasina yonlendirir).
        options.Events.OnRedirectToIdentityProvider = context =>
        {
            if (context.Properties.Items.TryGetValue("prompt", out var prompt) && !string.IsNullOrEmpty(prompt))
                context.ProtocolMessage.Prompt = prompt;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.MapDefaultEndpoints();


var cultureInfo = new CultureInfo("en-US");
CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(cultureInfo),
    SupportedCultures = [cultureInfo],
    SupportedUICultures = [cultureInfo]
});

// Configure the HTTP request pipeline.
app.UseExceptionHandler("/Error");

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
    .WithStaticAssets();

app.MapChatProxy();

// Urun gorseli proxy'si: DB'deki gateway-goreli /file/images/{name} yolunu ayni origin'den servis
// eder. file-api ic servis oldugundan (browser 'http://file-api'yi cozemez) WebApp sunucu tarafinda
// ceker ve geri yayinlar. Anonim: gorseller herkese acik (login gerektirmez).
app.MapGet("/file/images/{name}", async (string name, IHttpClientFactory factory, CancellationToken ct) =>
{
    var http = factory.CreateClient("file-images");
    var upstream = await http.GetAsync($"/images/{name}", ct);
    if (!upstream.IsSuccessStatusCode)
        return Results.NotFound();

    var contentType = upstream.Content.Headers.ContentType?.ToString() ?? "image/png";
    var bytes = await upstream.Content.ReadAsByteArrayAsync(ct);
    return Results.File(bytes, contentType);
}).AllowAnonymous();

app.Run();