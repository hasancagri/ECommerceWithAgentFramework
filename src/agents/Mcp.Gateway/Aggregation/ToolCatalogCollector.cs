using Microsoft.Extensions.Logging;

namespace Mcp.Gateway.Aggregation;

/// <summary>
/// 073: yüzeye uyan downstream'lerden ListTools'u LAZY (oturum anı) toplar + ad→BC registry kurar.
/// Startup-snapshot YOK (069 tuzağı): kısa-TTL cache; BC geç kalkarsa sonraki toplamada gelir. Erişilemez
/// BC atlanır (graceful degrade). Keşif makine token'ıyla (kullanıcı-bağımsız).
/// </summary>
public sealed class ToolCatalogCollector(
    FacadeOption facade,
    DiscoveryTokenSource tokens,
    DownstreamClientFactory clients,
    ILogger<ToolCatalogCollector> logger) : ISingletonDependency
{
    private sealed record Cached(IReadOnlyList<Tool> Tools, ToolRoutingRegistry Registry, DateTimeOffset ExpiresAt);

    private readonly Dictionary<string, Cached> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Yüzey için birleşik tool listesi + registry (cache'li). Registry CallTool yönlendirmesinde kullanılır.</summary>
    public async Task<(IReadOnlyList<Tool> Tools, ToolRoutingRegistry Registry)> GetAsync(string surface, CancellationToken ct)
    {
        if (_cache.TryGetValue(surface, out var hit) && DateTimeOffset.UtcNow < hit.ExpiresAt)
            return (hit.Tools, hit.Registry);

        await _gate.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue(surface, out hit) && DateTimeOffset.UtcNow < hit.ExpiresAt)
                return (hit.Tools, hit.Registry);

            var token = await tokens.GetTokenAsync(ct);
            var registry = new ToolRoutingRegistry();
            var tools = new List<Tool>();

            foreach (var bc in SurfaceFilter.ForSurface(facade.Downstreams, surface))
            {
                try
                {
                    // Keşif her downstream'e makine token'ı taşır (korumalı BC /mcp 401 vermesin; anonim
                    // BC token'ı yok sayar). RequiresUserAuth yalnız ÇAĞRI step-up'ını belirler, keşfi DEĞİL.
                    await using var client = await clients.CreateAsync(bc.McpUrl, token, null, ct);
                    var list = await client.ListToolsAsync(cancellationToken: ct);
                    foreach (var t in list)
                    {
                        if (registry.Add(t.ProtocolTool.Name, bc)) tools.Add(t.ProtocolTool);
                        else logger.LogWarning("Çift tool adı {Tool} — {Bc} yok sayıldı (ilk-sahip).", t.ProtocolTool.Name, bc.Name);
                    }
                }
                catch (Exception ex)
                {
                    // Graceful degrade: erişilemez BC bu turda atlanır; sonraki toplamada döner (kalıcı kayıp yok).
                    logger.LogWarning(ex, "Downstream {Bc} keşif atlandı (erişilemez).", bc.Name);
                }
            }

            var ttl = facade.CacheTtlSeconds <= 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(facade.CacheTtlSeconds);
            _cache[surface] = new Cached(tools, registry, DateTimeOffset.UtcNow.Add(ttl));
            return (tools, registry);
        }
        finally
        {
            _gate.Release();
        }
    }
}