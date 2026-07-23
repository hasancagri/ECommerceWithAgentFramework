using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;

namespace IngestionAgent.Infrastructure;

// MCP bağlantısı TEMBEL kurulur: açılışta değil, İLK tool çağrısında (startup, hedef servisin
// /mcp ucu hazır değil diye ölmesin). Kurulunca canlı tutulur; kurulamazsa çağrı fırlatır,
// kayıt Failed olur ve bir SONRAKİ çağrı yeniden dener. Bağlantı ANONİM (yazma yolu anonim, R5).
public sealed class McpConnection(IHttpClientFactory httpClientFactory, string serverName, string url)
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private McpClient? _client;

    public async Task<McpClient> GetAsync(CancellationToken ct)
    {
        if (_client is not null)
            return _client;

        await _lock.WaitAsync(ct);
        try
        {
            return _client ??= await McpClient.CreateAsync(
                new HttpClientTransport(
                    new HttpClientTransportOptions { Name = serverName, Endpoint = new Uri(url) },
                    httpClientFactory.CreateClient(HttpClients.McpNoToken),
                    NullLoggerFactory.Instance,
                    ownsHttpClient: false),
                cancellationToken: ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    // Hata gören çağrı cache'i düşürür: ölü bağlantı (servis restart'ı) sonsuza dek dönmesin;
    // bir SONRAKİ deneme temiz bağlantı kurar (S4 bulgusu). Yarışta kurulmuş yeni client korunur.
    public void Invalidate(McpClient client)
        => Interlocked.CompareExchange(ref _client, null, client);
}