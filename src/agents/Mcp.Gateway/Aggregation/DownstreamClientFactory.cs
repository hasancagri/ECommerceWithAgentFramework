using Microsoft.Extensions.Logging.Abstractions;

namespace Mcp.Gateway.Aggregation;

/// <summary>
/// 073: bir downstream BC /mcp'sine MCP client oturumu açar. Bearer (kullanıcı ya da makine) + anonim
/// X-User-Key başlıklarını taşıyan taze HttpClient (dev self-signed kabul). Çağrı başına taze oturum
/// (PerUserMcpTool deseni) — kullanıcı token'ı fasadda kalıcı saklanmaz.
/// </summary>
public sealed class DownstreamClientFactory : ISingletonDependency
{
    public async Task<McpClient> CreateAsync(string mcpUrl, string? bearer, string? userKey, CancellationToken ct)
    {
        var http = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });
        if (!string.IsNullOrWhiteSpace(bearer))
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        if (!string.IsNullOrWhiteSpace(userKey))
            http.DefaultRequestHeaders.Add("X-User-Key", userKey);

        var transport = new HttpClientTransport(
            new HttpClientTransportOptions { Name = "downstream", Endpoint = new Uri(mcpUrl) },
            http, NullLoggerFactory.Instance, ownsHttpClient: true);

        return await McpClient.CreateAsync(transport, cancellationToken: ct);
    }
}