using Identity.Server;
using Identity.Server.ApiKeys;
using Identity.Server.Connect;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// 030 RBAC: /Admin/* yönetim sayfaları cookie kullanıcısının admin rolünü ister (D3).
builder.Services.AddRazorPages(options =>
    options.Conventions.AuthorizeFolder("/Admin", "AdminRole"));

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

// House-style Options: appsettings section'lari tip'li POCO'ya bagla (config[...] magic-string yasak).
builder.Services.AddOptions<Identity.Server.Options.BootstrapAdmin>().BindConfiguration(nameof(Identity.Server.Options.BootstrapAdmin))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<Identity.Server.Options.BootstrapAdmin>(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Identity.Server.Options.BootstrapAdmin>>().Value);
builder.Services.AddOptions<Identity.Server.Options.ApiKeyAuth>().BindConfiguration(nameof(Identity.Server.Options.ApiKeyAuth))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<Identity.Server.Options.ApiKeyAuth>(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Identity.Server.Options.ApiKeyAuth>>().Value);

// 030 RBAC: token verme yolunda rol→scope demeti + admin yönetim servisi.
builder.Services.AddScoped<Identity.Server.Rbac.RoleScopeQuery>();
builder.Services.AddScoped<Identity.Server.Rbac.RoleAssignmentService>();

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
               .SetEndSessionEndpointUris("connect/logout")
               // 061: dış agent bağlantı koparma (FR-009) — OpenIddict kendi işler, passthrough yok.
               .SetRevocationEndpointUris("connect/revocation");

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

        // 061: discovery'ye registration_endpoint ekle (Claude Code DCR keşfi — R2).
        options.AddEventHandler(RegistrationEndpointMetadataHandler.Descriptor);

        // 061 (R5): RFC 8707 resource parametresi yok sayılır — audience scope eşlemesinden.
        options.AddEventHandler(IgnoreResourceParameterHandler.ForAuthorization.Descriptor);
        options.AddEventHandler(IgnoreResourceParameterHandler.ForToken.Descriptor);

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
{
    options.AddPolicy("apikeys.manage", policy =>
    {
        policy.AddAuthenticationSchemes("Bearer");
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("scope", "apikeys.manage");
    });

    // 030 RBAC: IdP admin UI guard'ı — Identity cookie principal'ında admin rolü (D3).
    // İlke V istisnası: IdP kendi iç yüzeyini rolle korur; downstream yalnız scope.
    options.AddPolicy("AdminRole", policy =>
        policy.RequireRole(Identity.Server.Rbac.RoleAssignmentService.AdminRole));

    // 061 logout: agent'ın kendi access token'ıyla (Bearer) korunur — scope aranmaz, kimlik yeter;
    // sub/client_id token'dan okunur, başka kullanıcı/client adına logout edilemez.
    options.AddPolicy("agent-authenticated", policy =>
    {
        policy.AddAuthenticationSchemes("Bearer");
        policy.RequireAuthenticatedUser();
    });
});

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
// 061: RFC 7591 DCR — dış agent (Claude Code) istemci kaydı (anonim uç).
app.MapRegisterEndpoint();
// 061 logout: agent chat'ten çıkış — kullanıcı+client authorization/token iptali (Bearer korumalı).
app.MapAgentLogoutEndpoint();

app.MapRazorPages().RequireAuthorization();
app.MapApiKeyEndpoints();

app.Run();