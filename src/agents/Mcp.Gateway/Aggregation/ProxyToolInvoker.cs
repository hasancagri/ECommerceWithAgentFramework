using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Mcp.Gateway.Aggregation;

/// <summary>
/// 073: CallTool proxy — adı registry'den çözer, sahibi BC'ye KULLANICI bearer'ı (+ anonim X-User-Key)
/// taşıyarak çağırır, sonucu döndürür. Bilinmeyen tool / erişilemez BC → MCP tool-error (fasad çökmez).
/// Kullanıcı token'ı kalıcı saklanmaz (çağrı başına HttpContext'ten okunur).
/// </summary>
public sealed class ProxyToolInvoker(
    ToolCatalogCollector collector,
    DownstreamClientFactory clients,
    IHttpContextAccessor httpContext,
    ILogger<ProxyToolInvoker> logger) : ISingletonDependency
{
    public async Task<CallToolResult> InvokeAsync(string surface, CallToolRequestParams request, CancellationToken ct)
    {
        var (_, registry) = await collector.GetAsync(surface, ct);
        var owner = registry.Resolve(request.Name);
        if (owner is null)
            return Error($"Bilinmeyen tool: {request.Name}");

        var (bearer, userKey) = ReadIdentity();
        try
        {
            await using var client = await clients.CreateAsync(owner.McpUrl, bearer, userKey, ct);
            return await client.CallToolAsync(request.Name, ToArgs(request.Arguments), cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "CallTool {Tool} → {Bc} proxy hatası.", request.Name, owner.Name);
            return Error($"'{request.Name}' şu an çalıştırılamadı ({owner.Name} erişilemez).");
        }
    }

    private (string? Bearer, string? UserKey) ReadIdentity()
    {
        var ctx = httpContext.HttpContext;
        if (ctx is null) return (null, null);
        var auth = ctx.Request.Headers.Authorization.FirstOrDefault();
        var bearer = auth?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true ? auth["Bearer ".Length..] : null;

        // Giriş varsa kullanıcı token'ı; yoksa ANONİM sepet için oturuma bağlı opak X-User-Key üret
        // (MCP-Session-Id'den deterministik → aynı Claude Desktop oturumu = aynı sepet; 057 + UserKey yan yolu).
        // Login (step-up) sonrası bearer gelir; anon sepet→kullanıcı devri (merge) ayrı iş (T021).
        var userKey = ctx.Request.Headers["X-User-Key"].FirstOrDefault();
        if (bearer is null && string.IsNullOrEmpty(userKey))
        {
            var sessionId = ctx.Request.Headers["Mcp-Session-Id"].FirstOrDefault();
            if (!string.IsNullOrEmpty(sessionId)) userKey = "anon-" + sessionId;
        }
        return (bearer, userKey);
    }

    private static IReadOnlyDictionary<string, object?>? ToArgs(IDictionary<string, JsonElement>? args)
        => args?.ToDictionary(kv => kv.Key, kv => (object?)kv.Value);

    private static CallToolResult Error(string message) => new()
    {
        IsError = true,
        Content = [new TextContentBlock { Text = message }]
    };
}