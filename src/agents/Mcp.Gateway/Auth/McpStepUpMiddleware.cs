namespace Mcp.Gateway.Auth;

/// <summary>
/// 073: satın-alma anında tek login (step-up). Anonim `/mcp` (RequireLoginUpfront=false) yolunda: gelen
/// JSON-RPC `tools/call` KORUMALI bir tool'a (owner.RequiresUserAuth) ve Authorization yoksa → HTTP 401 +
/// RFC 9728 challenge döner; mcp-remote OAuth'u O AN açar (login checkout/profil/geçmiş-sipariş eyleminde,
/// açılışta değil). Gezme/arama/anonim tool + tools/list + initialize DOKUNULMAZ. Upfront modda (true) ya
/// da /mcp-admin'de bu middleware devreye girmez (endpoint zaten RequireAuthorization).
/// </summary>
public sealed class McpStepUpMiddleware(
    RequestDelegate next,
    FacadeOption facade,
    ILogger<McpStepUpMiddleware> logger)
{
    private static readonly string[] CustomerScopes =
    [
        AuthorizationScopes.BasketRead, AuthorizationScopes.BasketWrite,
        AuthorizationScopes.OrderRead, AuthorizationScopes.OrderWrite,
        AuthorizationScopes.CustomerRead, AuthorizationScopes.PaymentRead, AuthorizationScopes.StorefrontRead
    ];

    public async Task InvokeAsync(HttpContext context, ToolCatalogCollector collector)
    {
        var path = context.Request.Path;
        var isCustomerMcp = path.StartsWithSegments("/mcp") && !path.StartsWithSegments("/mcp-admin");

        // Yalnız anonim müşteri /mcp POST'unda step-up değerlendir (upfront/admin endpoint'i kendi auth'unda).
        if (facade.RequireLoginUpfront || !isCustomerMcp || !HttpMethods.IsPost(context.Request.Method)
            || !string.IsNullOrEmpty(context.Request.Headers.Authorization))
        {
            await next(context);
            return;
        }

        var toolName = await ReadToolCallNameAsync(context.Request);
        if (toolName is null)
        {
            await next(context); // initialize / tools/list / anonim çağrı → dokunma
            return;
        }

        var (_, registry) = await collector.GetAsync(SurfaceFilter.Customer, context.RequestAborted);
        var owner = registry.Resolve(toolName);
        if (owner is null || !owner.RequiresUserAuth)
        {
            await next(context); // anonim (arama/katalog/anonim-sepet) tool → login gerekmez
            return;
        }

        // Korumalı tool + token yok → step-up: 401 + PRM challenge (mcp-remote OAuth açar).
        logger.LogInformation("Step-up: korumalı tool '{Tool}' anonim çağrıldı → login tetikleniyor.", toolName);
        var metadataUrl = $"{ExternalBase(context.Request)}/.well-known/oauth-protected-resource/mcp";
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate =
            $"Bearer resource_metadata=\"{metadataUrl}\", scope=\"{string.Join(' ', CustomerScopes)}\"";
    }

    // Gövdeyi buffer'la (MCP handler tekrar okur) + JSON-RPC tools/call ise tool adını çıkar.
    private static async Task<string?> ReadToolCallNameAsync(HttpRequest request)
    {
        if (request.ContentLength is null or 0) return null;
        request.EnableBuffering();
        try
        {
            using var doc = await JsonDocument.ParseAsync(request.Body, cancellationToken: request.HttpContext.RequestAborted);
            request.Body.Position = 0;
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (!root.TryGetProperty("method", out var m) || m.GetString() != "tools/call") return null;
            return root.TryGetProperty("params", out var p) && p.TryGetProperty("name", out var n)
                ? n.GetString()
                : null;
        }
        catch (JsonException)
        {
            request.Body.Position = 0;
            return null;
        }
    }

    private static string ExternalBase(HttpRequest request)
    {
        var proto = request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? request.Scheme;
        var host = request.Headers["X-Forwarded-Host"].FirstOrDefault() ?? request.Host.Value;
        return $"{proto}://{host}";
    }
}