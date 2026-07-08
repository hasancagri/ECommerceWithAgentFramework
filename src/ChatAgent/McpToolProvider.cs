using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;

namespace ChatAgent;

// MCP tool'larini keşfeder (ListTools) ve verilen allowlist'e gore filtreler. Token okumaz;
// Authorization, MCP HttpClient'ina takili TokenInjectingHandler tarafindan her cagriya
// iliştirilir (user token, yoksa m2m).
public interface IMcpToolProvider
{
    Task<IList<McpClientTool>> GetToolsAsync(
        string serverName, string url, IReadOnlyCollection<string> allowedTools, CancellationToken ct = default);
}

public sealed class RequestScopedMcpToolProvider(
    HttpClient httpClient,
    ILogger<RequestScopedMcpToolProvider> logger) : IMcpToolProvider
{
    public async Task<IList<McpClientTool>> GetToolsAsync(
        string serverName, string url, IReadOnlyCollection<string> allowedTools, CancellationToken ct = default)
    {
        try
        {
            var client = await McpClient.CreateAsync(
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

            return filtered;
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
        this IMcpToolProvider provider, params (string Name, string Url, string[] AllowedTools)[] servers)
    {
        List<AITool> tools = [];
        foreach (var (name, url, allowedTools) in servers)
            tools.AddRange(provider.GetToolsAsync(name, url, allowedTools).GetAwaiter().GetResult());
        return tools;
    }
}