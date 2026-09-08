namespace ChatAgent.InternalMCPs;

// MCP tool'larini boot'ta bir kez keşfeder (ListTools) ve allowlist'e gore filtreler; her
// sema icin bir PerUserMcpTool uretir. Keşif 061 korumali transport'larda MAKINE token'i tasir
// (TokenInjectingHandler + DiscoveryTokenSource; HttpContext yok => m2m fallback). Asil
// yetki CAGRI ANINDA cozulur: PerUserMcpTool her cagride, MCP'ye ozel named-client'a takili handler'in
// forward ettigi token'la taze bir session acar. Handler MCP'ye ozeldir: kendi server'larimiz
// Identity token'i tasir; dis MCP'ler (or. gmail) kendi client'iyla farkli/handler'siz baglanir.
// Tasarim: docs/superpowers/specs/2026-07-08-per-user-mcp-session-design.md
public interface IMcpToolProvider
{
    // attempts: null => varsayılan retry bütçesi (Aspire iç boot yarışı için). Dış MCP'ler
    // (Aspire'ın beklemediği, ör. DropShop) 1 verir — kapalıysa retry başlatmayı geciktirmesin.
    Task<IList<AITool>> GetToolsAsync(
        string serverName, string url, string clientName,
        IReadOnlyCollection<string> allowedTools, int? attempts = null, CancellationToken ct = default);
}

public sealed class McpToolProvider(
    IHttpClientFactory httpClientFactory,
    ILogger<McpToolProvider> logger) : IMcpToolProvider
{
    // 069 canlı bulgu: agent'lar STARTUP'ta kurulur (MAF Map* resolve eder) ve keşif O ANDA koşar;
    // hedef MCP henüz dinlemiyorsa (Aspire boot yarışı — WaitFor "Running" der, "dinliyor" demez)
    // tek deneme agent'ı KALICI tool'suz bırakır (singleton). Sınırlı retry yarışı kapatır; sınır
    // sonunda yine boş dönülür (gerçekten olmayan dış MCP için graceful-degrade korunur).
    // 20×3sn: soğuk açılışta (container recovery + Marten şema taraması) storefront 30sn'yi aşabiliyor
    // (canlı bulgu, 2026-09-08); asıl güvence AppHost WithHttpHealthCheck+WaitFor, bu bütçe backstop.
    private const int DiscoveryAttempts = 20;
    private static readonly TimeSpan DiscoveryRetryDelay = TimeSpan.FromSeconds(3);

    public async Task<IList<AITool>> GetToolsAsync(
        string serverName, string url, string clientName,
        IReadOnlyCollection<string> allowedTools, int? attempts = null, CancellationToken ct = default)
    {
        var maxAttempts = attempts ?? DiscoveryAttempts;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await DiscoverAsync(serverName, url, clientName, allowedTools, ct);
            }
            catch (Exception ex) when (IsAuthFailure(ex))
            {
                // 401/403 KALICIDIR (kesif kimligi eksik/yanlis) — retry bosuna startup'i uzatir.
                logger.LogWarning("MCP '{Server}' tool kesfi yetki hatasina carpti ({Error}); " +
                    "retry yok, bu server atlandi. DiscoveryAuth config'ini ve chat-agent-discovery seed'ini kontrol et.",
                    serverName, ex.Message);
                return [];
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logger.LogWarning("MCP '{Server}' tool kesfi basarisiz (deneme {Attempt}/{Max}): {Error} — tekrar denenecek.",
                    serverName, attempt, maxAttempts, ex.Message);
                await Task.Delay(DiscoveryRetryDelay, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning("MCP '{Server}' tool kesfi {Max} denemede basarisiz ({Error}); bu server atlandi.",
                    serverName, maxAttempts, ex.Message);
                return [];
            }
        }
    }

    private static bool IsAuthFailure(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException!)
            if (e is HttpRequestException { StatusCode: System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden })
                return true;
        return false;
    }

    private async Task<IList<AITool>> DiscoverAsync(
        string serverName, string url, string clientName,
        IReadOnlyCollection<string> allowedTools, CancellationToken ct)
    {
        {
            // MCP'ye ozel named-client: handler (Identity token / dis auth / hicbiri) bu client'ta yasar.
            var httpClient = httpClientFactory.CreateClient(clientName);

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

            // Ham McpClientTool yerine, cagriyi ayni named-client uzerinden taze session'a yonlendiren
            // PerUserMcpTool uret; boylece cagri da MCP'ye ozel handler'i tasir (keşifle ayni baglanti).
            return filtered
                .Select(AITool (t) => new PerUserMcpTool(t, httpClient, serverName, url, logger))
                .ToList();
        }
    }
}

public static class McpToolProviderExtensions
{
    // Verilen MCP server'larin allowlist'e gore filtrelenmis tool'larini tek listede toplar
    // (agent factory icinde). Her server girisi: izin verilen tool adlari + baglanacagi named-client.
    public static IList<AITool> CollectTools(
        this IMcpToolProvider provider,
        (string Name, string Url, string ClientName, string[] allowedTools)[] servers,
        int? attempts = null)
    {
        List<AITool> tools = [];
        foreach (var (name, url, clientName, allowedTools) in servers)
            tools.AddRange(provider.GetToolsAsync(name, url, clientName, allowedTools, attempts).GetAwaiter().GetResult());
        return tools;
    }
}