using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;

namespace ChatAgent;

// MCP tool'larini keşfeder (ListTools). Token okumaz; Authorization, MCP HttpClient'ina
// takili TokenInjectingHandler tarafindan her cagriya iliştirilir (user token, yoksa m2m).
public interface IMcpToolProvider
{
    Task<IList<McpClientTool>> GetToolsAsync(string serverName, string url, CancellationToken ct = default);
}

public sealed class RequestScopedMcpToolProvider(
    HttpClient httpClient,
    ILogger<RequestScopedMcpToolProvider> logger) : IMcpToolProvider
{
    public async Task<IList<McpClientTool>> GetToolsAsync(string serverName, string url, CancellationToken ct = default)
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

            return await client.ListToolsAsync(cancellationToken: ct);
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
    // Verilen MCP server'larin tool'larini tek listede toplar (agent factory icinde, request scope).
    public static IList<AITool> CollectTools(
        this IMcpToolProvider provider, params (string Name, string Url)[] servers)
    {
        List<AITool> tools = [];
        foreach (var (name, url) in servers)
            tools.AddRange(provider.GetToolsAsync(name, url).GetAwaiter().GetResult());
        return tools;
    }
}