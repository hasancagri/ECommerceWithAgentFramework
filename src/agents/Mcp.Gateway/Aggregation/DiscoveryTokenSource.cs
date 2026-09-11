namespace Mcp.Gateway.Aggregation;

/// <summary>
/// 073: ListTools keşfi için makine token'ı (client_credentials), süresine 30 sn kala yenilenir + cache.
/// Keşif kullanıcı-bağımsızdır (tool şeması herkese aynı). Secret boşsa null döner → keşif anonim
/// downstream'lerde çalışır, korumalı downstream keşfi atlanır (graceful). ChatAgent DiscoveryTokenSource emsali.
/// </summary>
public sealed class DiscoveryTokenSource(IdentityOption identity, FacadeOption facade) : ISingletonDependency
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
        if (string.IsNullOrWhiteSpace(facade.DiscoveryClientSecret))
            return null;
        if (_token is not null && DateTimeOffset.UtcNow < _expiresAt)
            return _token;

        await _gate.WaitAsync(ct);
        try
        {
            if (_token is not null && DateTimeOffset.UtcNow < _expiresAt)
                return _token;

            using var resp = await TokenHttp.PostAsync($"{identity.Address.TrimEnd('/')}/connect/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = facade.DiscoveryClientId,
                    ["client_secret"] = facade.DiscoveryClientSecret,
                    ["scope"] = facade.DiscoveryScope
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