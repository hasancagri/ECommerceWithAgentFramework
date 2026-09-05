namespace Common.Extensions;

// 061: RFC 9728 protected-resource keşfi — MCP ucu korumalı servisler için tek yerde (FR-002).
// İki parça: (1) /.well-known/oauth-protected-resource dokümanı, (2) 401 Bearer challenge'ına
// resource_metadata + scope parametreleri. Dış görünür adres (gateway) forwarded header'lardan.
public sealed record McpResourceMetadataOption(string ServiceSlug, string[] Scopes, string AuthorizationServer);

public static class McpResourceMetadataExtension
{
    public static IServiceCollection AddMcpResourceMetadata(this IServiceCollection services,
        IConfiguration configuration, string serviceSlug, params string[] scopes)
    {
        var identityOptions = configuration.GetSection(nameof(IdentityOption)).Get<IdentityOption>()!;
        var option = new McpResourceMetadataOption(serviceSlug, scopes, identityOptions.Address);
        services.AddSingleton(option);

        // Kimliksiz 401'e keşif parametreleri ekle (RFC 9728 §5): istemci OAuth zincirini buradan başlatır.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .PostConfigure(jwt =>
            {
                jwt.Events ??= new JwtBearerEvents();
                jwt.Events.OnChallenge = context =>
                {
                    context.HandleResponse();
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    var metadataUrl =
                        $"{ExternalBase(context.Request)}/.well-known/oauth-protected-resource/mcp/{option.ServiceSlug}";
                    context.Response.Headers.WWWAuthenticate =
                        $"Bearer resource_metadata=\"{metadataUrl}\", scope=\"{string.Join(' ', option.Scopes)}\"";
                    return Task.CompletedTask;
                };
            });

        return services;
    }

    public static WebApplication MapMcpResourceMetadata(this WebApplication app)
    {
        var option = app.Services.GetRequiredService<McpResourceMetadataOption>();

        // Gateway suffix'li yolu köke çevirir; doğrudan (gateway'siz) erişim için ikisi de açık.
        string[] paths =
        [
            "/.well-known/oauth-protected-resource",
            $"/.well-known/oauth-protected-resource/mcp/{option.ServiceSlug}",
        ];

        foreach (var path in paths)
            app.MapGet(path, (HttpContext http) => TypedResults.Json(new
            {
                resource = $"{ExternalBase(http.Request)}/mcp/{option.ServiceSlug}",
                authorization_servers = new[] { option.AuthorizationServer },
                scopes_supported = option.Scopes,
                bearer_methods_supported = new[] { "header" },
            })).AllowAnonymous();

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