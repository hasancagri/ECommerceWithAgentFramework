using Microsoft.Extensions.Logging.Abstractions;

namespace Mcp.Gateway.Aggregation;

/// <summary>
/// 073: bir downstream BC /mcp'sine MCP client oturumu açar. Client'ı IHttpClientFactory'den alır —
/// ServiceDefaults tüm factory client'larına SERVICE DISCOVERY ekler (ham HttpClient "http://basket-api"
/// adını çözemez → timeout). Bearer (kullanıcı/makine) + anonim X-User-Key başlıklarını taşır; çağrı
/// başına taze oturum (PerUserMcpTool deseni; kullanıcı token'ı kalıcı saklanmaz).
/// </summary>
public sealed class DownstreamClientFactory(IHttpClientFactory httpFactory) : ISingletonDependency
{
    public const string HttpClientName = "downstream";

    public async Task<McpClient> CreateAsync(string mcpUrl, string? bearer, string? userKey, CancellationToken ct)
    {
        var http = httpFactory.CreateClient(HttpClientName);
        if (!string.IsNullOrWhiteSpace(bearer))
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        if (!string.IsNullOrWhiteSpace(userKey))
            http.DefaultRequestHeaders.Add("X-User-Key", userKey);

        var transport = new HttpClientTransport(
            new HttpClientTransportOptions { Name = "downstream", Endpoint = new Uri(mcpUrl) },
            http, NullLoggerFactory.Instance, ownsHttpClient: false);

        return await McpClient.CreateAsync(transport, cancellationToken: ct);
    }
}
