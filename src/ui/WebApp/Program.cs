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

builder.Services.AddScoped<BasketService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<StockService>();
builder.Services.AddScoped<StorefrontService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<MerchantInformationService>();
builder.Services.AddScoped<ReviewsService>();
builder.Services.AddScoped<CatalogAdminService>();

builder.Services.AddScoped<AuthenticatedHttpClientHandler>();
builder.Services.AddScoped<AnonymousBasketIdHandler>();
builder.Services.AddExceptionHandler<UnauthorizedAccessExceptionHandler>();

// 057: girissiz kullanicida X-Anonymous-Id header'i eklenir (anonim sepet); login'de token gider.
builder.Services.AddRefitClient<IBasketRefitService>().ConfigureHttpClient(configure =>
    {
        configure.BaseAddress = new Uri("http://basket-api");
    }).AddHttpMessageHandler<AuthenticatedHttpClientHandler>()
    .AddHttpMessageHandler<AnonymousBasketIdHandler>();

// 057: login callback'inde anonim sepeti hesaba tasiyan merge cagrisi icin ciplak client
// (Refit zinciri kullanilamaz: callback aninda cookie user'i henuz olusmadi).
builder.Services.AddHttpClient("basket-merge", client =>
{
    client.BaseAddress = new Uri("http://basket-api");
});


builder.Services.AddRefitClient<IOrderRefitService>().ConfigureHttpClient(configure =>
    {
        configure.BaseAddress = new Uri("http://order-api");
    }).AddHttpMessageHandler<AuthenticatedHttpClientHandler>();

// 049: checkout girişi ayrı Checkout.Orchestrator'a (kullanıcı token'i BFF handler ile taşınır).
builder.Services.AddRefitClient<ICheckoutRefitService>().ConfigureHttpClient(configure =>
    {
        configure.BaseAddress = new Uri("http://checkout-orchestrator");
    }).AddHttpMessageHandler<AuthenticatedHttpClientHandler>();


builder.Services.AddRefitClient<IPaymentRefitService>().ConfigureHttpClient(configure =>
    {
        configure.BaseAddress = new Uri("http://payment-api");
    }).AddHttpMessageHandler<AuthenticatedHttpClientHandler>();


builder.Services.AddRefitClient<IStockRefitService>().ConfigureHttpClient(configure =>
    {
        configure.BaseAddress = new Uri("http://stock-api");
    }).AddHttpMessageHandler<AuthenticatedHttpClientHandler>();


// 006: ana sayfa vitrin listesi; okuma anonim, handler zinciri diğer istemcilerle tutarlılık için.
builder.Services.AddRefitClient<IStorefrontRefitService>().ConfigureHttpClient(configure =>
    {
        configure.BaseAddress = new Uri("http://storefront-api");
    }).AddHttpMessageHandler<AuthenticatedHttpClientHandler>();


// 022: Customer API (kayitli kart + adres defteri); kullanici token'iyla korunur.
builder.Services.AddRefitClient<ICustomerRefitService>().ConfigureHttpClient(configure =>
    {
        configure.BaseAddress = new Uri("http://customer-api");
    }).AddHttpMessageHandler<AuthenticatedHttpClientHandler>();


// 044: Reviews API — liste anonim, form/submit kullanici token'iyla (reviews.write).
builder.Services.AddRefitClient<IReviewsRefitService>().ConfigureHttpClient(configure =>
    {
        configure.BaseAddress = new Uri("http://reviews-api");
    }).AddHttpMessageHandler<AuthenticatedHttpClientHandler>();


// 058: Catalog yönetim penceresi — admin ürün düzenleme ekranları (catalog.write admin token'ıyla).
builder.Services.AddRefitClient<ICatalogRefitService>().ConfigureHttpClient(configure =>
    {
        configure.BaseAddress = new Uri("http://catalog-api");
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
        // basket
        options.Scope.Add("basket.read");
        options.Scope.Add("basket.write");
        // order
        options.Scope.Add("order.read");
        options.Scope.Add("order.write");
        // payment
        options.Scope.Add("payment.read");
        options.Scope.Add("payment.write");
        // 022: kayitli kart + adres defteri.
        options.Scope.Add("customer.read");
        options.Scope.Add("customer.write");
        // 033: merchant kimligi ekrani. Talep herkese, verilme role bagli (030): granted =
        // requested ∩ rol demeti — customer demetinde yok, yalniz admin token'ina biner.
        options.Scope.Add("merchant.credentials.write");
        // 044: urun yorumu yazma + uygunluk sorgusu (Order purchase-check gRPC'si de ayni scope).
        options.Scope.Add("reviews.write");
        // 054: kisisel ana sayfa feed'i (vitrin okumalar anonim kalir; yalniz bu uc kimlik ister).
        options.Scope.Add("storefront.read");
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

        // 057: login tamamlaninca anonim sepet hesaba tasinir (merge) ve anonim cookie silinir.
        // Merge hatasi login'i BOZMAZ: cookie kalir, sepet anonim kimlikte durmaya devam eder.
        options.Events.OnTicketReceived = async context =>
        {
            var anonymousId = AnonymousBasketId.Get(context.HttpContext);
            if (anonymousId is null) return;

            var accessToken = context.Properties?.GetTokenValue(OpenIdConnectParameterNames.AccessToken);
            if (string.IsNullOrEmpty(accessToken)) return;

            var client = context.HttpContext.RequestServices
                .GetRequiredService<IHttpClientFactory>().CreateClient("basket-merge");

            using var mergeRequest = new HttpRequestMessage(HttpMethod.Post, "api/v1/baskets/merge")
            {
                Content = JsonContent.Create(new { AnonymousId = anonymousId })
            };
            mergeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            try
            {
                var response = await client.SendAsync(mergeRequest);
                if (response.IsSuccessStatusCode)
                    AnonymousBasketId.Clear(context.HttpContext);
            }
            catch (Exception ex)
            {
                context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>()
                    .LogError(ex, "Anonim sepet birlesmesi basarisiz; login surduruluyor.");
            }
        };
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

app.Run();