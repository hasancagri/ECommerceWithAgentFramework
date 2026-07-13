namespace ProductEnrichmentAgent;

// Agent'in Catalog ve File bounded context'lerine tek yuzeyi: MCP tool cagrilari (anayasa I —
// DB'ye asla dokunmaz). Iki uzun-omurlu MCP session'i (catalog/file) gateway uzerinden acar;
// HttpClient'a takili ClientCredentialsTokenHandler her istege m2m token ekler. Tool sonuclari
// (FeatureXxxResultModel JSON) parse edilir. Gecici hatalar backoff ile yeniden denenir (FR-011).
public sealed class EnrichmentMcpClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<EnrichmentMcpClient> logger) : IAsyncDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private McpClient? _catalog;
    private McpClient? _file;

    private string GatewayUrl =>
        configuration["services:gateway:http:0"] ?? "http://localhost:5178";

    // --- Public tool cagrilari ---

    public async Task<IReadOnlyList<IncompleteProduct>> ListIncompleteAsync(int limit, CancellationToken ct)
    {
        var session = await CatalogAsync(ct);
        var result = await  CallAsync(session, CatalogTools.ListIncompleteProducts,
            new Dictionary<string, object?> { ["limit"] = limit }, ct);

        var model = Deserialize<FeatureListResultModel<IncompleteProduct>>(result);
        return model is { IsSuccess: true, Data: { } data } ? data : [];
    }

    public async Task<bool> SetDescriptionAsync(Guid id, string description, CancellationToken ct)
    {
        var session = await CatalogAsync(ct);
        var result = await CallAsync(session, CatalogTools.SetProductDescription,
            new Dictionary<string, object?> { ["id"] = id, ["description"] = description }, ct);
        return Deserialize<FeatureResultModel>(result) is { IsSuccess: true };
    }

    public async Task<string?> UploadImageAsync(Guid id, byte[] png, CancellationToken ct)
    {
        var session = await FileAsync(ct);
        var result = await CallAsync(session, FileTools.UploadProductImage,
            new Dictionary<string, object?>
            {
                ["productId"] = id,
                ["contentBase64"] = Convert.ToBase64String(png),
                ["contentType"] = "image/png",
            }, ct);

        var model = Deserialize<FeatureObjectResultModel<UploadResult>>(result);
        return model is { IsSuccess: true, Data.Url: { } url } ? url : null;
    }

    public async Task<bool> SetImageAsync(Guid id, string imageUrl, CancellationToken ct)
    {
        var session = await CatalogAsync(ct);
        var result = await CallAsync(session, CatalogTools.SetProductImage,
            new Dictionary<string, object?> { ["id"] = id, ["imageUrl"] = imageUrl }, ct);
        return Deserialize<FeatureResultModel>(result) is { IsSuccess: true };
    }

    // --- Session yonetimi ---

    private async Task<McpClient> CatalogAsync(CancellationToken ct) =>
        _catalog ??= await CreateSessionAsync(McpServers.Catalog, ct);

    private async Task<McpClient> FileAsync(CancellationToken ct) =>
        _file ??= await CreateSessionAsync(McpServers.File, ct);

    private async Task<McpClient> CreateSessionAsync(string server, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var existing = server == McpServers.Catalog ? _catalog : _file;
            if (existing is not null) return existing;

            var http = httpClientFactory.CreateClient(AgentConstants.McpHttpClient);
            var url = $"{GatewayUrl}/mcp/{server}";
            var client = await McpClient.CreateAsync(
                new HttpClientTransport(
                    new HttpClientTransportOptions { Name = server, Endpoint = new Uri(url) },
                    http,
                    NullLoggerFactory.Instance,
                    ownsHttpClient: false),
                cancellationToken: ct);
            return client;
        }
        finally
        {
            _lock.Release();
        }
    }

    // --- Cagri + retry (FR-011: gecici hatalar backoff ile yeniden denenir) ---

    private async Task<CallToolResult> CallAsync(
        McpClient session, string tool, IReadOnlyDictionary<string, object?> args, CancellationToken ct)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await session.CallToolAsync(tool, args, cancellationToken: ct);
            }
            catch (Exception ex) when (attempt < maxAttempts && ex is not OperationCanceledException)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt)); // 2s, 4s
                logger.LogWarning(ex, "MCP '{Tool}' cagrisi basarisiz (deneme {Attempt}/{Max}); {Delay}s sonra tekrar.",
                    tool, attempt, maxAttempts, delay.TotalSeconds);
                await Task.Delay(delay, ct);
            }
        }
    }

    // --- Structured deserialize ---

    // Web varsayilanlari (camelCase + case-insensitive) + enum'u sayi VEYA string olarak oku
    // (MCP tel formati ne olursa olsun): boylece BrandType tek satirda cozulur, elle map gerekmez.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    // Tool sonucunu (FeatureXxxResultModel JSON) tipli modele cevirir: once StructuredContent,
    // yoksa ilk metin blogu. Cozulemezse default (null) doner; cagiran IsSuccess'e bakar.
    private static T? Deserialize<T>(CallToolResult result)
    {
        if (result.StructuredContent is { } structured)
            return structured.Deserialize<T>(JsonOptions);

        var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text;
        if (string.IsNullOrWhiteSpace(text)) return default;

        try { return JsonSerializer.Deserialize<T>(text, JsonOptions); }
        catch { return default; }
    }

    public async ValueTask DisposeAsync()
    {
        if (_catalog is not null) await _catalog.DisposeAsync();
        if (_file is not null) await _file.DisposeAsync();
        _lock.Dispose();
    }
}