using Mcp.Gateway.Aggregation;
using Mcp.Gateway.Dependencies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

// 085: fasad yüzey scope demeti = Mcp.Gateway.FacadeScopes.All (TEK uç, /mcp-admin öldü).

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
// (bare /mcp): RFC 9728 challenge + authorization_servers OpenIddict issuer'ıyla birebir (trailing slash —
// katı mcp-remote issuer mismatch'i önler, [[mcp-remote-issuer-slash-gotcha]]).
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
                var metadataUrl = $"{ExternalBase(ctx.Request)}/.well-known/oauth-protected-resource/mcp";
                ctx.Response.Headers.WWWAuthenticate =
                    $"Bearer resource_metadata=\"{metadataUrl}\", scope=\"{string.Join(' ', Mcp.Gateway.FacadeScopes.All)}\"";
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

// Fasad = dinamik MCP server: ListTools (lazy toplama, oturum token'ıyla) + CallTool (ad→BC token-forward proxy).
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithListToolsHandler(async (ctx, ct) =>
    {
        var tools = await ctx.Services!.GetRequiredService<ToolCatalogCollector>().GetToolsAsync(ct);
        return new ListToolsResult { Tools = [.. tools] };
    })
    .WithCallToolHandler(async (ctx, ct) =>
        await ctx.Services!.GetRequiredService<ProxyToolInvoker>().InvokeAsync(ctx.Params!, ct));

var app = builder.Build();
app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseAuthorization();

// TEK uç, tek login (upfront, kalıcı — kullanıcı kararı 085): bağlanınca login şart, anonim gez/step-up
// modu yok. Müşteri + admin aynı ucta (/mcp-admin öldü).
app.MapMcp("/mcp").RequireAuthorization();

// Fasad PRM (RFC 9728): resource = fasad ucu; scopes_supported = müşteri+admin UNION (FR-006 — tavan-üstü
// talep IdP'de sessizce elenir, bağlantı kırılmaz; bkz. R3 + contracts/mcp-surface.md).
app.MapGet("/.well-known/oauth-protected-resource/mcp",
    (HttpContext http) => Results.Json(Prm(http))).AllowAnonymous();

await app.RunAsync();
return;

object Prm(HttpContext http) => new
{
    resource = $"{ExternalBase(http.Request)}/mcp",
    authorization_servers = new[] { identityOption.Address.TrimEnd('/') + "/" },
    scopes_supported = Mcp.Gateway.FacadeScopes.All,
    bearer_methods_supported = new[] { "header" },
};

// Dış görünür taban: gateway'in eklediği X-Forwarded-Proto/Host; yoksa isteğin kendisi.
static string ExternalBase(HttpRequest request)
{
    var proto = request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? request.Scheme;
    var host = request.Headers["X-Forwarded-Host"].FirstOrDefault() ?? request.Host.Value;
    return $"{proto}://{host}";
}