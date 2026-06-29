using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;

namespace AgentOrchestrator;

// Istek basina MCP araclarini saglar. Scoped olarak kaydedilir; cagrildiginda
// o anki HttpContext token'iyla MCP client kurar ve tool listesini doner.
// Paylasimli/startup oturumu YOK: her istek kendi kimligiyle kendi oturumunu acar.
public interface IMcpToolProvider
{
    Task<IList<McpClientTool>> GetToolsAsync(string serverName, string url, CancellationToken ct = default);
}

public sealed class RequestScopedMcpToolProvider(
    IHttpContextAccessor accessor,
    HttpClient httpClient,
    ILogger<RequestScopedMcpToolProvider> logger) : IMcpToolProvider
{
    public async Task<IList<McpClientTool>> GetToolsAsync(string serverName, string url, CancellationToken ct = default)
    {
        try
        {
            // HttpClient constructor'dan (IHttpClientFactory) gelir: handler havuzlanir, istek
            // basina taze instance. O anki kullanicinin bearer'ini default header'a basariz =>
            // bu client'in tum cagrilari (tool invoke dahil) token'i tasir. Remove-once: ayni
            // instance ile birden cok server kesfedilirse header tekrarlanmasin.
            httpClient.DefaultRequestHeaders.Remove("Authorization");
            if (accessor.HttpContext?.Request.Headers.Authorization.ToString() is
                {
                    Length: > 0
                } bearer)
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", bearer);

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
            // Yetkisiz / erisilemez: bu istek icin o servisin araclarini bos gec.
            // Uygulama dusmez; bir sonraki (yetkili) istek tekrar dener.
            logger.LogWarning(ex, "MCP '{Server}' tool kesfi basarisiz; bu istek icin atlandi.", serverName);
            return [];
        }
    }
}