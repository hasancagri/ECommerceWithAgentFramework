using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;

namespace IngestionAgent.Infrastructure;

// Tool keşfi TEMBEL + tek seferlik (R4): açılışta değil İLK mesajda ListTools (startup, hedef
// servis hazır değil diye ölmesin — eski McpConnector gerekçesi). Başarılı keşif cache'lenir.
// ChatAgent'tan bilinçli fark: keşif hatası boş listeyle DEVAM ETMEZ, FIRLATIR — yazma yolunda
// tool'suz agent sessiz veri kaybıdır; hata adım başarısızlığına, oradan retry/DLQ'ya gider.
public sealed class McpToolCatalog(
    IHttpClientFactory httpClientFactory,
    string serverName,
    string url,
    IReadOnlyCollection<string> allowedTools,
    ILogger logger)
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IList<AITool>? _tools;

    public async Task<IList<AITool>> GetAsync(CancellationToken ct)
    {
        if (_tools is not null)
            return _tools;

        await _lock.WaitAsync(ct);
        try
        {
            if (_tools is not null)
                return _tools;

            var httpClient = httpClientFactory.CreateClient(McpClients.NoToken);

            // Keşif client'ı yalnız ListTools yapar; iş bitince dispose (çağrılar taze session açar).
            await using var client = await McpClient.CreateAsync(
                new HttpClientTransport(
                    new HttpClientTransportOptions { Name = serverName, Endpoint = new Uri(url) },
                    httpClient,
                    NullLoggerFactory.Instance,
                    ownsHttpClient: false),
                cancellationToken: ct);

            var all = await client.ListToolsAsync(cancellationToken: ct);
            var filtered = all.Where(t => allowedTools.Contains(t.Name)).ToList();

            // Allowlist'te olup sunucuda olmayan tool = yazım hatası/rename; yazıcı bu tool'suz
            // ÇALIŞAMAZ → uyarı değil hata (fail-safe, FR-009).
            var missing = allowedTools.Where(n => all.All(t => t.Name != n)).ToList();
            if (missing.Count > 0)
                throw new InvalidOperationException(
                    $"MCP '{serverName}': allowlist'teki tool(lar) sunucuda bulunamadı: {string.Join(", ", missing)}");

            logger.LogInformation("MCP '{Server}': {Count} tool keşfedildi ({Tools}).",
                serverName, filtered.Count, string.Join(", ", filtered.Select(t => t.Name)));

            return _tools = filtered
                .Select(AITool (t) => new AnonymousMcpTool(t, httpClient, serverName, url, logger))
                .ToList();
        }
        finally
        {
            _lock.Release();
        }
    }
}