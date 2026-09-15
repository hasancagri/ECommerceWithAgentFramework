using Mcp.Gateway.Aggregation;
using Mcp.Gateway.Dependencies;
using Mcp.Gateway.Routing;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

// 073: fasad yüzey scope demetleri (challenge + PRM'de ilan; gerçek yetki downstream'de + kullanıcı rolünde).
string[] customerScopes =
[
    AuthorizationScopes.BasketRead, AuthorizationScopes.BasketWrite,
    AuthorizationScopes.OrderRead, AuthorizationScopes.OrderWrite,
    AuthorizationScopes.CustomerRead, AuthorizationScopes.CustomerWrite,
    AuthorizationScopes.PaymentRead, AuthorizationScopes.StorefrontRead
];
string[] adminScopes =
[
    AuthorizationScopes.CatalogWrite, AuthorizationScopes.StockWrite, AuthorizationScopes.MerchantCredentialsWrite
];

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddOptions<IdentityOption>().BindConfiguration(nameof(IdentityOption))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<IdentityOption>(sp => sp.GetRequiredService<IOptions<IdentityOption>>().Value);
builder.Services.AddOptions<FacadeOption>().BindConfiguration(nameof(FacadeOption));
builder.Services.AddSingleton<FacadeOption>(sp => sp.GetRequiredService<IOptions<FacadeOption>>().Value);

builder.Services.AddHttpContextAccessor();
builder.Services.AddAllDependencies();

// Fasad = proxy: gelen kullanıcı token'ının audience'ı DOWNSTREAM API'lerindir → ValidateAudience=false
// (imza+issuer+ömür doğrulanır; scope zorlaması downstream'de, token aynen forward). Fasad PRM özel
// (bare /mcp): RFC 9728 challenge yol-prefix'ine göre + authorization_servers OpenIddict issuer'ıyla
// birebir (trailing slash — katı mcp-remote issuer mismatch'i önler, [[mcp-remote-issuer-slash-gotcha]]).
var identityOption = builder.Configuration.GetSection(nameof(IdentityOption)).Get<IdentityOption>()!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = identityOption.Address;
        options.RequireHttpsMetadata = false;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false,
            ValidateIssuer = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = ctx =>
            {
                ctx.HandleResponse();
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                var isAdmin = ctx.Request.Path.StartsWithSegments("/mcp-admin");
                var slug = isAdmin ? "mcp-admin" : "mcp";
                var scopes = isAdmin ? adminScopes : customerScopes;
                var metadataUrl = $"{ExternalBase(ctx.Request)}/.well-known/oauth-protected-resource/{slug}";
                ctx.Response.Headers.WWWAuthenticate =
                    $"Bearer resource_metadata=\"{metadataUrl}\", scope=\"{string.Join(' ', scopes)}\"";
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

// Fasad = dinamik MCP server: ListTools (lazy toplama) + CallTool (ad→BC token-forward proxy).
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithListToolsHandler(async (ctx, ct) =>
    {
        var sp = ctx.Services!;
        var surface = CurrentSurface(sp);
        var (tools, _) = await sp.GetRequiredService<ToolCatalogCollector>().GetAsync(surface, ct);
        return new ListToolsResult { Tools = [.. tools] };
    })
    .WithCallToolHandler(async (ctx, ct) =>
    {
        var sp = ctx.Services!;
        var surface = CurrentSurface(sp);
        return await sp.GetRequiredService<ProxyToolInvoker>().InvokeAsync(surface, ctx.Params!, ct);
    });

var app = builder.Build();
app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseAuthorization();

// Step-up: anonim /mcp'de korumalı tool çağrısı + token yok → 401 + PRM (login satın almada, açılışta değil).
app.UseMiddleware<Mcp.Gateway.Auth.McpStepUpMiddleware>();

// Müşteri ucu: RequireLoginUpfront=true → bağlanınca tek login. false → ANONİM bağlan/gez (arama/katalog
// login'siz); korumalı tool çağrısı downstream'de 401 → tool-error (satın almada login gerekir). Yönetim
// ucu HER ZAMAN korumalı.
var facade = app.Services.GetRequiredService<FacadeOption>();
var customerMcp = app.MapMcp("/mcp");
if (facade.RequireLoginUpfront) customerMcp.RequireAuthorization();
app.MapMcp("/mcp-admin").RequireAuthorization();

// Fasad PRM (RFC 9728): resource = fasad ucu; authorization_servers = OpenIddict issuer (trailing slash).
app.MapGet("/.well-known/oauth-protected-resource/mcp",
    (HttpContext http) => Results.Json(Prm(http, "mcp", customerScopes))).AllowAnonymous();
app.MapGet("/.well-known/oauth-protected-resource/mcp-admin",
    (HttpContext http) => Results.Json(Prm(http, "mcp-admin", adminScopes))).AllowAnonymous();

await app.RunAsync();
return;

object Prm(HttpContext http, string slug, string[] scopes) => new
{
    resource = $"{ExternalBase(http.Request)}/{slug}",
    authorization_servers = new[] { identityOption.Address.TrimEnd('/') + "/" },
    scopes_supported = scopes,
    bearer_methods_supported = new[] { "header" }
};

// Dış görünür taban: gateway'in eklediği X-Forwarded-Proto/Host; yoksa isteğin kendisi.
static string ExternalBase(HttpRequest request)
{
    var proto = request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? request.Scheme;
    var host = request.Headers["X-Forwarded-Host"].FirstOrDefault() ?? request.Host.Value;
    return $"{proto}://{host}";
}

// İstek yolundan yüzey (müşteri/admin) — RequestContext.Services request scope'undan HttpContext.
static string CurrentSurface(IServiceProvider sp)
{
    var path = sp.GetService<IHttpContextAccessor>()?.HttpContext?.Request.Path.Value ?? "/mcp";
    return SurfaceFilter.FromPath(path);
}
