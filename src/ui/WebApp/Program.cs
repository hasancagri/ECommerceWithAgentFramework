using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Localization;
using System.Globalization;
using WebApp.ExceptionHandlers;
using WebApp.Authentication;
using WebApp.Chat;


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

builder.Services.AddScoped<TokenService>();
builder.Services.AddHttpContextAccessor();

// 066: müşteri BFF servisleri söküldü (agent-only); yalnız admin yönetim servisleri kalır.
builder.Services.AddScoped<StockService>();
builder.Services.AddScoped<MerchantInformationService>();
builder.Services.AddScoped<CatalogAdminService>();

builder.Services.AddScoped<AuthenticatedHttpClientHandler>();
builder.Services.AddExceptionHandler<UnauthorizedAccessExceptionHandler>();


// 058: admin ürün düzenleme ekranları stok penceresi (stock.write admin token'ıyla).
builder.Services.AddRefitClient<IStockRefitService>().ConfigureHttpClient(configure =>
    {
        configure.BaseAddress = new Uri("http://stock-api");
    }).AddHttpMessageHandler<AuthenticatedHttpClientHandler>();


// 058: Catalog yönetim penceresi — admin ürün düzenleme ekranları (catalog.write admin token'ıyla).
builder.Services.AddRefitClient<ICatalogRefitService>().ConfigureHttpClient(configure =>
    {
        configure.BaseAddress = new Uri("http://catalog-api");
    }).AddHttpMessageHandler<AuthenticatedHttpClientHandler>();


// 066: Customer API artık yalnız admin merchant onboarding için (merchant.credentials.write);
// adres/cüzdan müşteri yüzeyi söküldü. MerchantInformationService bu istemciyi kullanır.
builder.Services.AddRefitClient<ICustomerRefitService>().ConfigureHttpClient(configure =>
    {
        configure.BaseAddress = new Uri("http://customer-api");
    }).AddHttpMessageHandler<AuthenticatedHttpClientHandler>();


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
        // 066: müşteri alışveriş scope'ları söküldü (basket/order/payment/customer/reviews/library/
        // storefront) — o BFF istemcileri kaldırıldı; müşteri işlemleri artık yalnız agent/MCP yolunda.
        // 033: merchant kimligi ekrani. Talep herkese, verilme role bagli (030): granted =
        // requested ∩ rol demeti — customer demetinde yok, yalniz admin token'ina biner.
        options.Scope.Add("merchant.credentials.write");
        // 058: admin urun duzenleme ekranlari. Talep herkese, verilme role bagli (030 deseni:
        // granted = requested ∩ rol demeti) — ikisi de yalniz admin token'ina biner.
        options.Scope.Add("catalog.write");
        options.Scope.Add("stock.write");

        // Token'daki "name"/"role" claim'lerini standart tiplere esle (policy'ler icin).
        // 030 RBAC: MapInboundClaims (default true) gelen "role"u ClaimTypes.Role (uzun URI)'ye
        // cevirir; RoleClaimType de ona esitlenmeli yoksa User.IsInRole("admin") eslesmez.
        // AuthenticatedHttpClientHandler da ClaimTypes.Role kullanir → tutarli.
        options.TokenValidationParameters = new()
        {
            NameClaimType = "name",
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
        };

        // SignUp akisinda set edilen "prompt=create"i authorize istegine tasi
        // (Identity.Server bunu gorup kayit sayfasina yonlendirir).
        options.Events.OnRedirectToIdentityProvider = context =>
        {
            if (context.Properties.Items.TryGetValue("prompt", out var prompt) && !string.IsNullOrEmpty(prompt))
                context.ProtocolMessage.Prompt = prompt;
            return Task.CompletedTask;
        };
        // 066: anonim-sepet merge (OnTicketReceived) söküldü — sepet UI'ı yok; sepet artık agent/MCP
        // yolunda. Anonim→login merge o yolda ilgisiz.
    });

builder.Services.AddAuthorization();

// 033: DropShop kayıt istemcisi (imperatif MCP) + bellek-içi credential deposu KALDIRILDI —
// MCP yalnız agent yüzeyi; kayıt ChatAgent'la, kimlik Customer.Api'de kalıcı.

var app = builder.Build();

app.MapDefaultEndpoints();


var cultureInfo = new CultureInfo("tr-TR");
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

// 066: kaldırılan müşteri ekranlarına eski derin bağlantılar ham 500 değil temiz sonuç versin —
// eşleşmeyen route köke (mağaza asistanı) yönlendirilir; gerçek endpoint'ler (/chat/*, /Admin/*,
// /Auth/*, static) zaten map'li olduğundan fallback yalnız eşleşmeyende çalışır.
app.MapFallback(context =>
{
    context.Response.Redirect("/");
    return Task.CompletedTask;
});

app.Run();