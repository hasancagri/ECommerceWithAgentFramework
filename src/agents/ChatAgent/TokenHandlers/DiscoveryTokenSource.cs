namespace ChatAgent.TokenHandlers;

// Açılış MCP keşfi için makine token'ı (client_credentials) alır ve süresi dolana dek (−30 sn pay)
// cache'ler. Option null (config yok) => null döner, keşif anonim kalır (graceful-degrade).
// Identity dev cert self-signed => bu istemcide sertifika doğrulaması kapalı (dev).
public sealed class DiscoveryTokenSource(DiscoveryAuthOption? option)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    private static readonly HttpClient TokenHttp = new(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });

    public async Task<string?> GetTokenAsync(CancellationToken ct)
    {
        if (option is null)
            return null;
        if (_token is not null && DateTimeOffset.UtcNow < _expiresAt)
            return _token;

        await _gate.WaitAsync(ct);
        try
        {
            if (_token is not null && DateTimeOffset.UtcNow < _expiresAt)
                return _token;

            using var resp = await TokenHttp.PostAsync(option.TokenEndpoint,
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = option.ClientId,
                    ["client_secret"] = option.ClientSecret,
                    ["scope"] = option.Scope
                }), ct);
            resp.EnsureSuccessStatusCode();

            using var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var root = json.RootElement;
            _token = root.GetProperty("access_token").GetString()!;
            var expiresIn = root.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 300;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn - 30);
            return _token;
        }
        finally
        {
            _gate.Release();
        }
    }
}
