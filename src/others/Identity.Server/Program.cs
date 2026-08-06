using Identity.Server;
using Identity.Server.ApiKeys;
using Identity.Server.Connect;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using static OpenIddict.Abstractions.OpenIddictConstants;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

// Aspire çalışma anında enjekte eder; design-time (migration üretimi) için fallback.
var connectionString = builder.Configuration.GetConnectionString("identityDb")
                       ?? "Host=localhost;Port=5432;Database=identityDb;Username=postgres;Password=postgres";

var migrationsAssembly = typeof(Program).Assembly.GetName().Name;

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(connectionString, sql => sql.MigrationsAssembly(migrationsAssembly));
    // OpenIddict EF Core store'ları aynı context'i kullanır.
    options.UseOpenIddict();
});

builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddScoped<ApiKeyService>();

// Login/challenge yolları + "beni hatırla" süresi (persistent cookie bu kadar yaşar).
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = Identity.Server.Pages.Login.LoginOptions.RememberMeLoginDuration;
});

builder.Services.AddOpenIddict()
    .AddCore(options =>
        options.UseEntityFrameworkCore().UseDbContext<ApplicationDbContext>())
    .AddServer(options =>
    {
        // Issuer birebir korunur (servislerin Authority değeri buna bağlı).
        options.SetIssuer(new Uri("https://localhost:5001"));

        options.SetAuthorizationEndpointUris("connect/authorize")
               .SetTokenEndpointUris("connect/token")
               .SetUserInfoEndpointUris("connect/userinfo")
               .SetEndSessionEndpointUris("connect/logout");

        options.AllowAuthorizationCodeFlow()
               .AllowClientCredentialsFlow()
               .AllowRefreshTokenFlow();

        options.RegisterScopes([.. Config.AllApiScopes, .. Config.IdentityScopes]);

        // WebApp "Sign Up" akışı prompt=create gönderir; kayıtlı olmasa 400 ile reddedilirdi.
        options.RegisterPromptValues(PromptValues.Create);

        options.AddDevelopmentEncryptionCertificate()
               .AddDevelopmentSigningCertificate();

        // Access token düz imzalı JWT olsun ki servislerin JwtBearer'ı çözebilsin.
        options.DisableAccessTokenEncryption();

        // R3: access token scope claim'ini çoklu değere çevir (Duende paritesi).
        options.AddEventHandler(ScopeClaimArrayHandler.Descriptor);

        options.UseAspNetCore()
               .EnableAuthorizationEndpointPassthrough()
               .EnableTokenEndpointPassthrough()
               .EnableUserInfoEndpointPassthrough()
               .EnableEndSessionEndpointPassthrough()
               .EnableStatusCodePagesIntegration();
    });

// Açılışta idempotent client + scope seed.
builder.Services.AddHostedService<SeedHostedService>();

// Admin API uçları (issue/revoke) için kendi token'larımızı doğrulayan JWT bearer.
// Default şema (Identity cookie) değişmez; policy Bearer şemasını açıkça ister.
var apiAuthority = builder.Configuration["ApiKeyAuth:Authority"] ?? "https://localhost:5001";
builder.Services.AddAuthentication()
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = apiAuthority;
        options.RequireHttpsMetadata = false;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false,
        };
    });

builder.Services.AddAuthorization(options =>
    options.AddPolicy("apikeys.manage", policy =>
    {
        policy.AddAuthenticationSchemes("Bearer");
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("scope", "apikeys.manage");
    }));

var app = builder.Build();

// Açılışta migration'ları uygula (dev kolaylığı; Postgres Aspire ile hazır olur).
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// OIDC uçları (OpenIddict passthrough ile ASP.NET Core'da işlenir).
app.MapAuthorizeEndpoint();
app.MapTokenEndpoint();
app.MapUserInfoEndpoint();
app.MapLogoutEndpoint();

app.MapRazorPages().RequireAuthorization();
app.MapApiKeyEndpoints();

app.Run();