using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Mcp.Gateway.Aggregation;

/// <summary>
/// 085 R1/R2: TEK uç toplaması. İki bağımsız gerçek-kaynak:
/// (1) <see cref="GetToolsAsync"/> — tools/list, OTURUM SAHİBİNİN token'ıyla toplar; her downstream BC
///     zaten kendi scope'una göre budanmış liste döner (fasat süzme yapmaz — İLKE V ruhu, yetki bilgisi
///     fasada sızmaz). Cache anahtarı scope-parmakizi (token'daki sıralı scope kümesi; token'sız "anon").
/// (2) <see cref="GetRegistryAsync"/> — CallTool yönlendirme adı→BC kaydı, m2m TAM-katalog taramasından
///     (makine token'ı tüm admin scope'larını taşır — appsettings DiscoveryScope), scope-bağımsız TEK cache.
/// Startup-snapshot YOK (069 tuzağı): kısa-TTL cache; BC geç kalkarsa sonraki toplamada gelir. Erişilemez
/// BC atlanır (graceful degrade).
/// </summary>
public sealed class ToolCatalogCollector(
    FacadeOption facade,
    DiscoveryTokenSource tokens,
    DownstreamClientFactory clients,
    IHttpContextAccessor httpContext,
    ILogger<ToolCatalogCollector> logger) : ISingletonDependency
{
    private sealed record ToolsCached(IReadOnlyList<Tool> Tools, DateTimeOffset ExpiresAt);
    private sealed record RegistryCached(ToolRoutingRegistry Registry, DateTimeOffset ExpiresAt);

    private readonly Dictionary<string, ToolsCached> _toolsCache = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _toolsGate = new(1, 1);
    private RegistryCached? _registryCache;
    private readonly SemaphoreSlim _registryGate = new(1, 1);

    public async Task<IReadOnlyList<Tool>> GetToolsAsync(CancellationToken ct)
    {
        var key = ScopeFingerprint(httpContext.HttpContext?.User.FindAll("scope").Select(c => c.Value));
        if (_toolsCache.TryGetValue(key, out var hit) && DateTimeOffset.UtcNow < hit.ExpiresAt)
            return hit.Tools;

        await _toolsGate.WaitAsync(ct);
        try
        {
            if (_toolsCache.TryGetValue(key, out hit) && DateTimeOffset.UtcNow < hit.ExpiresAt)
                return hit.Tools;

            var (bearer, userKey) = ReadIdentity();
            var tools = new List<Tool>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var bc in facade.Downstreams)
            {
                try
                {
                    await using var client = await clients.CreateAsync(bc.McpUrl, bearer, userKey, ct);
                    foreach (var t in await client.ListToolsAsync(cancellationToken: ct))
                        if (seen.Add(t.ProtocolTool.Name)) tools.Add(t.ProtocolTool);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Downstream {Bc} keşif atlandı (erişilemez).", bc.Name);
                }
            }

            var cached = new ToolsCached(tools, DateTimeOffset.UtcNow.Add(Ttl()));
            _toolsCache[key] = cached;
            return cached.Tools;
        }
        finally
        {
            _toolsGate.Release();
        }
    }

    public async Task<ToolRoutingRegistry> GetRegistryAsync(CancellationToken ct)
    {
        if (_registryCache is { } hit && DateTimeOffset.UtcNow < hit.ExpiresAt)
            return hit.Registry;

        await _registryGate.WaitAsync(ct);
        try
        {
            if (_registryCache is { } hit2 && DateTimeOffset.UtcNow < hit2.ExpiresAt)
                return hit2.Registry;

            var token = await tokens.GetTokenAsync(ct);
            var registry = new ToolRoutingRegistry();
            foreach (var bc in facade.Downstreams)
            {
                try
                {
                    await using var client = await clients.CreateAsync(bc.McpUrl, token, null, ct);
                    foreach (var t in await client.ListToolsAsync(cancellationToken: ct))
                        if (!registry.Add(t.ProtocolTool.Name, bc))
                            logger.LogWarning("Çift tool adı {Tool} — {Bc} yok sayıldı (ilk-sahip).", t.ProtocolTool.Name, bc.Name);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Downstream {Bc} keşif atlandı (erişilemez).", bc.Name);
                }
            }

            var cached = new RegistryCached(registry, DateTimeOffset.UtcNow.Add(Ttl()));
            _registryCache = cached;
            return cached.Registry;
        }
        finally
        {
            _registryGate.Release();
        }
    }

    private TimeSpan Ttl() => facade.CacheTtlSeconds <= 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(facade.CacheTtlSeconds);

    private (string? Bearer, string? UserKey) ReadIdentity()
    {
        var ctx = httpContext.HttpContext;
        if (ctx is null) return (null, null);
        var auth = ctx.Request.Headers.Authorization.FirstOrDefault();
        var bearer = auth?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true ? auth["Bearer ".Length..] : null;
        var userKey = ctx.Request.Headers["X-User-Key"].FirstOrDefault();
        return (bearer, userKey);
    }

    // 085 R2 (saf, İLKE VI test-first): token'daki sıralı scope kümesi (aynı setin sahibi herkes cache
    // paylaşır); token'sız/boş = "anon".
    public static string ScopeFingerprint(IEnumerable<string>? scopes)
    {
        var arr = scopes?.ToArray();
        return arr is { Length: > 0 } ? string.Join(' ', arr.OrderBy(s => s, StringComparer.Ordinal)) : "anon";
    }
}