using Mcp.Gateway.Aggregation;
using Mcp.Gateway.Dependencies;
using Mcp.Gateway.Routing;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Options (tip'li).
builder.Services.AddOptions<IdentityOption>().BindConfiguration(nameof(IdentityOption))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<IdentityOption>(sp => sp.GetRequiredService<IOptions<IdentityOption>>().Value);
builder.Services.AddOptions<FacadeOption>().BindConfiguration(nameof(FacadeOption));
builder.Services.AddSingleton<FacadeOption>(sp => sp.GetRequiredService<IOptions<FacadeOption>>().Value);

builder.Services.AddHttpContextAccessor();
builder.Services.AddAllDependencies();

// Fasad = proxy: gelen kullanıcı token'ının audience'ı DOWNSTREAM API'lerindir (basket.api/order.api...),
// fasadın kendi audience'ı yok → ValidateAudience=false. İmza+issuer+ömür doğrulanır; scope zorlaması
// downstream'de (token aynen forward). Bu yüzden paylaşılan (audience-zorunlu) extension yerine minimal setup.
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
    });
builder.Services.AddAuthorization();

// İki PRM: /mcp (müşteri demeti) + /mcp-admin (yönetim demeti). RFC 9728 challenge yol-prefix'ine göre seçer.
builder.Services.AddMcpResourceMetadata(builder.Configuration, "",
    AuthorizationScopes.BasketRead, AuthorizationScopes.BasketWrite,
    AuthorizationScopes.OrderRead, AuthorizationScopes.OrderWrite,
    AuthorizationScopes.CustomerRead, AuthorizationScopes.PaymentRead, AuthorizationScopes.StorefrontRead);
builder.Services.AddMcpAdminResourceMetadata(builder.Configuration, "",
    AuthorizationScopes.CatalogWrite, AuthorizationScopes.StockWrite, AuthorizationScopes.MerchantCredentialsWrite);

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

// Taban: RequireLoginUpfront=true → iki uç da korumalı (bağlanınca tek login). Anonim/step-up yolu
// (false) sonraki artım — canlı step-up doğrulanınca açılır (kullanıcı kararı: önce dene, olmazsa upfront).
app.MapMcp("/mcp").RequireAuthorization();
app.MapMcp("/mcp-admin").RequireAuthorization();
app.MapMcpResourceMetadata();

await app.RunAsync();
return;

// İstek yolundan yüzey (müşteri/admin) — RequestContext.Services request scope'undan HttpContext.
static string CurrentSurface(IServiceProvider sp)
{
    var path = sp.GetService<IHttpContextAccessor>()?.HttpContext?.Request.Path.Value ?? "/mcp";
    return SurfaceFilter.FromPath(path);
}