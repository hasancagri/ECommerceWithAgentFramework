using System.Net.Http.Headers;

namespace Customer.Api.Infrastructure.Tokenization;

/// <summary>
/// Gerçek <see cref="ICardTokenizer"/>: DropShop gateway card vault (017) çağırır. PAN'ı gateway'e
/// gönderir, yalnız opak token alır; <c>brand</c>/<c>last4</c>/<c>bin</c>'i PAN'dan LOKAL türetir
/// (gateway yalnız token döner — 017 kararı #2). CVV vault'a gitmez (saklanmaz). Merchant kimliği
/// <see cref="MerchantInformation"/>'dan; token <see cref="IMerchantTokenProvider"/>'dan. DB okur →
/// Scoped. PAN/CVV hiçbir yere yazılmaz/loglanmaz.
/// </summary>
public sealed class GatewayCardTokenizer : ICardTokenizer, IScopedDependency
{
    private readonly IQuerySession _session;
    private readonly IMerchantTokenProvider _tokenProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DropShopVaultOption _option;
    private readonly ILogger<GatewayCardTokenizer> _logger;

    public GatewayCardTokenizer(
        IQuerySession session,
        IMerchantTokenProvider tokenProvider,
        IHttpClientFactory httpClientFactory,
        DropShopVaultOption option,
        ILogger<GatewayCardTokenizer> logger)
    {
        _session = session;
        _tokenProvider = tokenProvider;
        _httpClientFactory = httpClientFactory;
        _option = option;
        _logger = logger;
    }

    public async Task<TokenizeResult> TokenizeAsync(
        string pan, string cvv, int expiryMonth, int expiryYear, CancellationToken ct)
    {
        var merchant = await _session.Query<MerchantInformation>().FirstOrDefaultAsync(ct);
        if (merchant is null)
        {
            _logger.LogWarning("Vault tokenize: MerchantInformation kaydı yok (fail-closed).");
            return Fail();
        }

        // Gateway yalnız token döner; gösterilebilir alanlar PAN'dan lokal türetilir (017 kararı #2).
        var digits = new string((pan ?? string.Empty).Where(char.IsDigit).ToArray());
        var last4 = digits.Length >= 4 ? digits[^4..] : digits;
        var bin = digits.Length >= 6 ? digits[..6] : null;
        var brand = digits.Length > 0 && digits[0] == '4' ? "Visa"
            : digits.Length > 0 && digits[0] == '5' ? "Mastercard"
            : "Card";
        var expiry = $"{expiryMonth:D2}/{expiryYear % 100:D2}";

        try
        {
            var token = await _tokenProvider.GetTokenAsync(merchant.MerchantId, merchant.MerchantKey, ct);
            var http = _httpClientFactory.CreateClient("dropshop-vault");
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var url = $"{_option.VaultBaseUrl.TrimEnd('/')}/api/v1.0/merchants/{merchant.MerchantId}/vault/cards";
            using var resp = await http.PostAsJsonAsync(url,
                new { pan = digits, expiry, holderName = "CARD HOLDER" }, ct);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Vault tokenize başarısız: {Status}", resp.StatusCode);
                return Fail();
            }

            using var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var vaultToken = json.RootElement.TryGetProperty("token", out var t) ? t.GetString() : null;
            if (string.IsNullOrWhiteSpace(vaultToken))
                return Fail();

            return new TokenizeResult(true, vaultToken, brand, last4, null, bin);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Vault tokenize hata (fail-closed).");
            return Fail();
        }
    }

    public async Task RevokeAsync(string token, CancellationToken ct)
    {
        // Fail-open: çağıran (DeleteCard) zaten yutuyor; hata yerel silmeyi bloklamaz.
        var merchant = await _session.Query<MerchantInformation>().FirstOrDefaultAsync(ct);
        if (merchant is null)
            return;

        var accessToken = await _tokenProvider.GetTokenAsync(merchant.MerchantId, merchant.MerchantKey, ct);
        var http = _httpClientFactory.CreateClient("dropshop-vault");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var url = $"{_option.VaultBaseUrl.TrimEnd('/')}/api/v1.0/merchants/{merchant.MerchantId}/vault/cards/{token}";
        using var resp = await http.DeleteAsync(url, ct);
        if (!resp.IsSuccessStatusCode)
            _logger.LogWarning("Vault revoke başarısız (fail-open): {Status}", resp.StatusCode);
    }

    private static TokenizeResult Fail() =>
        new(false, null, null, null, CustomerResourceConstants.INVALID_OPERATION_ERROR, null);
}
