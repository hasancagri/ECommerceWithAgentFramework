namespace Common.Extensions;

// 061: RFC 9728 protected-resource keşfi — MCP ucu korumalı servisler için tek yerde (FR-002).
// İki parça: (1) /.well-known/oauth-protected-resource dokümanı, (2) 401 Bearer challenge'ına
// resource_metadata + scope parametreleri. Dış görünür adres (gateway) forwarded header'lardan.
// 070: aynı serviste İKİNCİ korumalı yüzey (/mcp-admin) — PathPrefix ayrımı; challenge isteğin
// yoluna göre doğru metadata'yı seçer (admin uç → admin scope'ları).
public sealed record McpResourceMetadataOption(
    string ServiceSlug, string[] Scopes, string AuthorizationServer, string PathPrefix = "mcp");

public static class McpResourceMetadataExtension
{
    public static IServiceCollection AddMcpResourceMetadata(this IServiceCollection services,
        IConfiguration configuration, string serviceSlug, params string[] scopes)
        => AddCore(services, configuration, serviceSlug, "mcp", scopes);

    // 070: /mcp-admin ucunun metadata'sı (admin scope demeti). Aynı serviste /mcp metadata'sıyla
    // birlikte yaşayabilir; challenge yol-prefix'ine göre seçer.
    public static IServiceCollection AddMcpAdminResourceMetadata(this IServiceCollection services,
        IConfiguration configuration, string serviceSlug, params string[] scopes)
        => AddCore(services, configuration, serviceSlug, "mcp-admin", scopes);

    private static IServiceCollection AddCore(IServiceCollection services,
        IConfiguration configuration, string serviceSlug, string pathPrefix, string[] scopes)
    {
        var identityOptions = configuration.GetSection(nameof(IdentityOption)).Get<IdentityOption>()!;
        services.AddSingleton(new McpResourceMetadataOption(serviceSlug, scopes, identityOptions.Address, pathPrefix));

        // Kimliksiz 401'e keşif parametreleri ekle (RFC 9728 §5): istemci OAuth zincirini buradan başlatır.
        // Delegate idempotent: birden çok kayıt üst üste yazar ama mantık hep "kayıtlı TÜM option'lardan
        // yola göre seç" olduğundan sonuç aynıdır.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .PostConfigure(jwt =>
            {
                jwt.Events ??= new JwtBearerEvents();
                jwt.Events.OnChallenge = context =>
                {
                    context.HandleResponse();
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;

                    var option = Resolve(context.HttpContext);
                    var metadataUrl =
                        $"{ExternalBase(context.Request)}/.well-known/oauth-protected-resource/{option.PathPrefix}/{option.ServiceSlug}";
                    context.Response.Headers.WWWAuthenticate =
                        $"Bearer resource_metadata=\"{metadataUrl}\", scope=\"{string.Join(' ', option.Scopes)}\"";
                    return Task.CompletedTask;
                };
            });

        return services;
    }

    // İsteğin yoluna göre doğru metadata: /mcp-admin* → admin option'ı, aksi hâlde standart.
    private static McpResourceMetadataOption Resolve(HttpContext http)
    {
        var options = http.RequestServices.GetServices<McpResourceMetadataOption>().ToArray();
        var isAdmin = http.Request.Path.StartsWithSegments("/mcp-admin");
        return options.FirstOrDefault(o => (o.PathPrefix == "mcp-admin") == isAdmin) ?? options.First();
    }

    public static WebApplication MapMcpResourceMetadata(this WebApplication app)
    {
        var options = app.Services.GetServices<McpResourceMetadataOption>().ToArray();

        // Gateway suffix'li yolu köke çevirmez; doğrudan (gateway'siz) erişim için çıplak yol da açık —
        // çıplak yol İLK kayıtlı option'ı döner (tek-option servislerde 061 davranışı birebir).
        foreach (var (option, index) in options.Select((o, i) => (o, i)))
        {
            var paths = new List<string> { $"/.well-known/oauth-protected-resource/{option.PathPrefix}/{option.ServiceSlug}" };
            if (index == 0)
                paths.Add("/.well-known/oauth-protected-resource");

            foreach (var path in paths)
                app.MapGet(path, (HttpContext http) => TypedResults.Json(new
                {
                    resource = $"{ExternalBase(http.Request)}/{option.PathPrefix}/{option.ServiceSlug}",
                    // RFC 8414 §3.3: authorization_servers girdisi, AS metadata'sındaki `issuer` ile BİREBİR
                    // eşleşmeli. OpenIddict issuer'ı trailing slash'le biter (Uri normalizasyonu) → burada da
                    // slash'i garanti et (yoksa katı mcp-remote "issuer mismatch" ile bağlantıyı düşürür).
                    authorization_servers = new[] { option.AuthorizationServer.TrimEnd('/') + "/" },
                    scopes_supported = option.Scopes,
                    bearer_methods_supported = new[] { "header" },
                })).AllowAnonymous();
        }

        return app;
    }

    // Dış görünür taban: gateway'in eklediği X-Forwarded-Proto/Host; yoksa isteğin kendisi.
    private static string ExternalBase(HttpRequest request)
    {
        var proto = request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? request.Scheme;
        var host = request.Headers["X-Forwarded-Host"].FirstOrDefault() ?? request.Host.Value;
        return $"{proto}://{host}";
    }
}