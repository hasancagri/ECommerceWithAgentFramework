using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;

namespace ChatAgent;

// MCP tool'larini boot'ta bir kez ANONIM keşfeder (ListTools) ve allowlist'e gore filtreler; her
// sema icin bir PerUserMcpTool uretir. Keşif token okumaz (transport acik, allowlist statik). Asil
// yetki CAGRI ANINDA cozulur: PerUserMcpTool her cagride, HttpClient'a takili TokenInjectingHandler'in
// forward ettigi kullanici token'iyla taze bir user-bound session acar.
// Tasarim: docs/superpowers/specs/2026-07-08-per-user-mcp-session-design.md
public interface IMcpToolProvider
{
    Task<IList<AITool>> GetToolsAsync(
        string serverName, string url, IReadOnlyCollection<string> allowedTools, CancellationToken ct = default);
}

public sealed class McpToolProvider(
    HttpClient httpClient,
    ILogger<McpToolProvider> logger) : IMcpToolProvider
{
    public async Task<IList<AITool>> GetToolsAsync(
        string serverName, string url, IReadOnlyCollection<string> allowedTools, CancellationToken ct = default)
    {
        try
        {
            // Kesif client'i: yalnizca ListTools (sema) yapar, hic CallTool yapmaz; is bitince dispose.
            await using var client = await McpClient.CreateAsync(
                new HttpClientTransport(
                    new HttpClientTransportOptions { Name = serverName, Endpoint = new Uri(url) },
                    httpClient,
                    NullLoggerFactory.Instance,
                    ownsHttpClient: false),
                cancellationToken: ct);

            var all = await client.ListToolsAsync(cancellationToken: ct);

            // Yalnizca allowlist'teki tool'lari birak; bilinmeyen/yeni tool asla eklenmez (fail-safe).
            var filtered = all.Where(t => allowedTools.Contains(t.Name)).ToList();

            // Allowlist'te olup sunucunun sunmadigi isimler = yazim hatasi/rename; sessiz kaybi onlemek icin uyar.
            var missing = allowedTools.Where(n => all.All(t => t.Name != n)).ToList();
            if (missing.Count > 0)
                logger.LogWarning("MCP '{Server}': allowlist'teki tool(lar) sunucuda bulunamadi: {Missing}",
                    serverName, string.Join(", ", missing));

            // Ham McpClientTool yerine, cagriyi kullanici-bagli taze session'a yonlendiren PerUserMcpTool uret.
            return filtered
                .Select(AITool (t) => new PerUserMcpTool(t, httpClient, serverName, url, logger))
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "MCP '{Server}' tool kesfi basarisiz; bu istek icin atlandi.", serverName);
            return [];
        }
    }
}

public static class McpToolProviderExtensions
{
    // Verilen MCP server'larin allowlist'e gore filtrelenmis tool'larini tek listede toplar
    // (agent factory icinde). Her server girisi izin verilen tool adlarini ZORUNLU belirtir.
    public static IList<AITool> CollectTools(
        this IMcpToolProvider provider, params (string Name, string Url, string[] allowedTools)[] servers)
    {
        List<AITool> tools = [];
        foreach (var (name, url, allowedTools) in servers)
            tools.AddRange(provider.GetToolsAsync(name, url, allowedTools).GetAwaiter().GetResult());
        return tools;
    }
}