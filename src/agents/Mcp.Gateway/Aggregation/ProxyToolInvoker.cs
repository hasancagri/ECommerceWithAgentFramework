using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Mcp.Gateway.Aggregation;

/// <summary>
/// 073/085: CallTool proxy — adı registry'den (m2m tam-katalog taramasından, R2) çözer, sahibi BC'ye
/// KULLANICI bearer'ı (+ anonim X-User-Key) taşıyarak çağırır, sonucu döndürür. Bilinmeyen tool /
/// erişilemez BC → MCP tool-error (fasad çökmez). Kullanıcı token'ı kalıcı saklanmaz (çağrı başına
/// HttpContext'ten okunur). Listede olmayan/yetkisiz tool çağrısı BC'de reddedilir (403 son savunma).
/// </summary>
public sealed class ProxyToolInvoker(
    ToolCatalogCollector collector,
    DownstreamClientFactory clients,
    IHttpContextAccessor httpContext,
    ILogger<ProxyToolInvoker> logger) : ISingletonDependency
{
    public async Task<CallToolResult> InvokeAsync(CallToolRequestParams request, CancellationToken ct)
    {
        var registry = await collector.GetRegistryAsync(ct);
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

        // Yalnız GERÇEK (issue edilmiş) X-User-Key varsa taşı — uydurma key downstream ApiKey resolve'da
        // "Invalid or revoked" 401 verir ve anonim endpoint'i bile bozar. Anonim aramada hiçbir kimlik
        // gönderilmez (storefront/catalog anonim çalışır). Anonim sepet gerçek anon-key üretimi ister (gap).
        var userKey = ctx.Request.Headers["X-User-Key"].FirstOrDefault();
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