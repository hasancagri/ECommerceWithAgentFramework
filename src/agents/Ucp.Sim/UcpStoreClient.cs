namespace Ucp.Sim;

/// <summary>
/// Simülatörün mağaza /ucp cephesine giden istemcisi. client_credentials (ucp-platform) token'ı alır +
/// cache'ler, checkout uçlarını çağırır, ham JSON gövdeyi döner (LLM'e sunulur). Test aracı — üretim
/// dayanıklılık/yeniden-deneme aranmaz. Store base service discovery ile (http://ucp-api).
/// </summary>
public sealed class UcpStoreClient(HttpClient http, IHttpClientFactory factory, UcpSimOptions opts)
{
    private string? _token;
    private DateTimeOffset _expiresAt;

    // SC-006: mağaza UCP cephesi snake_case bekler — hem property hem Dictionary key snake'e çevrilir.
    private static readonly JsonSerializerOptions SnakeJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public async Task<string> CreateSessionAsync(object body, CancellationToken ct)
        => await PostAsync("ucp/checkout_sessions", body, null, ct);

    public async Task<string> UpdateSessionAsync(string id, object body, CancellationToken ct)
        => await PostAsync($"ucp/checkout_sessions/{id}", body, null, ct);

    public async Task<string> GetSessionAsync(string id, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"ucp/checkout_sessions/{id}");
        return await SendAsync(req, ct);
    }

    public async Task<string> CompleteSessionAsync(string id, string idempotencyKey, CancellationToken ct)
        => await PostAsync($"ucp/checkout_sessions/{id}/complete", new { }, idempotencyKey, ct);

    public async Task<string> CancelSessionAsync(string id, string? reason, CancellationToken ct)
        => await PostAsync($"ucp/checkout_sessions/{id}/cancel", new { reason }, null, ct);

    // US2 keşif/katalog: anonim (token gerektirmez).
    public Task<string> DiscoverAsync(CancellationToken ct) => GetAnonAsync(".well-known/ucp", ct);
    public Task<string> SearchCatalogAsync(string q, CancellationToken ct)
        => GetAnonAsync($"ucp/catalog?q={Uri.EscapeDataString(q)}", ct);
    public Task<string> LookupProductAsync(string id, CancellationToken ct) => GetAnonAsync($"ucp/catalog/{id}", ct);

    private async Task<string> GetAnonAsync(string path, CancellationToken ct)
    {
        using var res = await http.GetAsync(path, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        return $"HTTP {(int)res.StatusCode}\n{body}";
    }

    private async Task<string> PostAsync(string path, object body, string? idempotencyKey, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body, options: SnakeJson) };
        if (idempotencyKey is not null) req.Headers.Add("Idempotency-Key", idempotencyKey);
        return await SendAsync(req, ct);
    }

    private async Task<string> SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetTokenAsync(ct));

        // US4: RequireSignatures=on yolunu test etmek için giden isteği RFC 9421 ile imzala.
        if (opts.SignRequests && !string.IsNullOrWhiteSpace(opts.PrivateKeyPem))
            await SignAsync(req, ct);

        using var res = await http.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        return $"HTTP {(int)res.StatusCode}\n{body}";
    }

    private async Task SignAsync(HttpRequestMessage req, CancellationToken ct)
    {
        var body = req.Content is null ? [] : await req.Content.ReadAsByteArrayAsync(ct);
        var absoluteUri = new Uri(http.BaseAddress!, req.RequestUri!).ToString();
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var digest = ContentDigest.Compute(body);
        var paramsValue = SignatureBaseBuilder.BuildParams(created, opts.KeyId, opts.Algorithm);
        var signatureBase = SignatureBaseBuilder.Build(req.Method.Method, absoluteUri, digest, paramsValue);
        var sig = Es256Signature.Sign(signatureBase, opts.PrivateKeyPem);

        req.Headers.TryAddWithoutValidation("Content-Digest", digest);
        req.Headers.TryAddWithoutValidation("Signature-Input", $"{UcpSigningKeys.SignatureLabel}={paramsValue}");
        req.Headers.TryAddWithoutValidation("Signature", $"{UcpSigningKeys.SignatureLabel}=:{Convert.ToBase64String(sig)}:");
    }

    private async Task<string> GetTokenAsync(CancellationToken ct)
    {
        if (_token is not null && DateTimeOffset.UtcNow < _expiresAt.AddSeconds(-30))
            return _token;

        using var idClient = factory.CreateClient("identity");
        using var res = await idClient.PostAsync($"{opts.IdentityAddress.TrimEnd('/')}/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = opts.ClientId,
                ["client_secret"] = opts.ClientSecret,
                ["scope"] = opts.Scope
            }), ct);
        res.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        _token = json.RootElement.GetProperty("access_token").GetString()!;
        var expiresIn = json.RootElement.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;
        _expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
        return _token;
    }
}