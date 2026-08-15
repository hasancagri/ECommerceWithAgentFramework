using System.Text.Json;
using Customer.Api.Options;

namespace Customer.Api.Domains.Wallets.Tokenization;

/// <summary>
/// DropShop merchant OAuth token sağlayıcı: client_credentials (client_id=merchantId,
/// client_secret=MerchantKey, scope <c>cards.write payment.charge</c>) → gateway Identity connect/token.
/// Tek token vault (kart saklama) + 033 çekim/taksit için ortak kullanılır (superset scope). Token
/// bellek-içi cache'lenir (exp − 30 sn yenile). State tutar → Singleton. Dönen token'da
/// <c>merchant_id</c> claim'i route ile eşleşir (gateway MerchantScoped fail-closed).
/// </summary>
public interface IMerchantTokenProvider
{
    Task<string> GetTokenAsync(Guid merchantId, string merchantKey, CancellationToken ct);
}

public sealed class MerchantTokenProvider : IMerchantTokenProvider, ISingletonDependency
{
    private readonly DropShopVaultOption _option;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private string? _token;
    private DateTimeOffset _expiresAt;

    public MerchantTokenProvider(DropShopVaultOption option, IHttpClientFactory httpClientFactory)
    {
        _option = option;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<string> GetTokenAsync(Guid merchantId, string merchantKey, CancellationToken ct)
    {
        if (_token is not null && DateTimeOffset.UtcNow < _expiresAt)
            return _token;

        await _gate.WaitAsync(ct);
        try
        {
            if (_token is not null && DateTimeOffset.UtcNow < _expiresAt)
                return _token;

            var http = _httpClientFactory.CreateClient("dropshop-vault");
            using var resp = await http.PostAsync(_option.TokenEndpoint,
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = merchantId.ToString(),
                    ["client_secret"] = merchantKey,
                    ["scope"] = "cards.write payment.charge"
                }), ct);
            resp.EnsureSuccessStatusCode();

            using var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            _token = json.RootElement.GetProperty("access_token").GetString()!;
            var expiresIn = json.RootElement.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 900;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn - 30);
            return _token;
        }
        finally
        {
            _gate.Release();
        }
    }
}
