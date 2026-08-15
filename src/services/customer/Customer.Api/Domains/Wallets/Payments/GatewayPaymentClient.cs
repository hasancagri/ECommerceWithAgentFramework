using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Customer.Api.Domains.MerchantInformations;
using Customer.Api.Domains.Wallets.Tokenization;
using Customer.Api.Options;
using Microsoft.Extensions.Logging;

namespace Customer.Api.Domains.Wallets.Payments;

/// <summary>
/// 033: DropShop gateway ödeme uçlarını (Payment.Api US2 taksit + US1 kayıtlı kartla çekim) çağıran
/// istemci. Vault token + BIN Customer.Api'de kilitli (WebApp/LLM görmez); gateway'e merchant OAuth
/// (scope <c>payment.charge</c>) ile gider. Merchant kimliği <see cref="MerchantInformation"/>'dan,
/// token <see cref="IMerchantTokenProvider"/>'dan. DB okur → Scoped. PAN/CVV yok (vault modeli);
/// yalnız opak token + BIN. <see cref="GatewayCardTokenizer"/> kardeşi (aynı named-client + auth).
/// </summary>
public interface IGatewayPaymentClient
{
    Task<IReadOnlyList<GatewayInstallmentOption>?> GetInstallmentsAsync(string bin, decimal price, CancellationToken ct);
    Task<GatewayChargeResult?> ChargeAsync(GatewayChargeInput input, CancellationToken ct);
}

public sealed record GatewayInstallmentOption(int InstallmentNumber, decimal TotalPrice);

public sealed record GatewayChargeInput(
    string VaultToken, decimal Price, decimal PaidPrice, int Installment,
    string BuyerName, string BuyerSurname, string BuyerEmail, string BuyerGsm);

public sealed record GatewayChargeResult(
    Guid PaymentId, string ProviderPaymentId, string Status, decimal Price, decimal PaidPrice, int Installment);

public sealed class GatewayPaymentClient : IGatewayPaymentClient, IScopedDependency
{
    private readonly IQuerySession _session;
    private readonly IMerchantTokenProvider _tokenProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DropShopVaultOption _option;
    private readonly ILogger<GatewayPaymentClient> _logger;

    public GatewayPaymentClient(
        IQuerySession session,
        IMerchantTokenProvider tokenProvider,
        IHttpClientFactory httpClientFactory,
        DropShopVaultOption option,
        ILogger<GatewayPaymentClient> logger)
    {
        _session = session;
        _tokenProvider = tokenProvider;
        _httpClientFactory = httpClientFactory;
        _option = option;
        _logger = logger;
    }

    public async Task<IReadOnlyList<GatewayInstallmentOption>?> GetInstallmentsAsync(
        string bin, decimal price, CancellationToken ct)
    {
        var merchant = await _session.Query<MerchantInformation>().FirstOrDefaultAsync(ct);
        if (merchant is null)
        {
            _logger.LogWarning("Taksit sorgusu: MerchantInformation kaydı yok.");
            return null;
        }

        try
        {
            var http = await AuthorizedClientAsync(merchant, ct);
            var url = $"{_option.VaultBaseUrl.TrimEnd('/')}/api/v1.0/merchants/{merchant.MerchantId}/payments/installment-options";
            using var resp = await http.PostAsJsonAsync(url, new { bin, price }, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Taksit sorgusu başarısız: {Status}", resp.StatusCode);
                return null;
            }

            using var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            if (!json.RootElement.TryGetProperty("installmentDetails", out var arr) ||
                arr.ValueKind != JsonValueKind.Array)
                return null;

            var list = new List<GatewayInstallmentOption>();
            foreach (var el in arr.EnumerateArray())
                list.Add(new GatewayInstallmentOption(
                    el.GetProperty("installmentNumber").GetInt32(),
                    el.GetProperty("totalPrice").GetDecimal()));
            return list;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Taksit sorgusu hata.");
            return null;
        }
    }

    public async Task<GatewayChargeResult?> ChargeAsync(GatewayChargeInput input, CancellationToken ct)
    {
        var merchant = await _session.Query<MerchantInformation>().FirstOrDefaultAsync(ct);
        if (merchant is null)
        {
            _logger.LogWarning("Çekim: MerchantInformation kaydı yok.");
            return null;
        }

        try
        {
            var http = await AuthorizedClientAsync(merchant, ct);
            var url = $"{_option.VaultBaseUrl.TrimEnd('/')}/api/v1.0/merchants/{merchant.MerchantId}/payments/";
            var body = new
            {
                vaultToken = input.VaultToken,
                price = input.Price,
                paidPrice = input.PaidPrice,
                installment = input.Installment,
                buyer = new
                {
                    name = input.BuyerName,
                    surname = input.BuyerSurname,
                    email = input.BuyerEmail,
                    gsmNumber = input.BuyerGsm,
                    identityNumber = "11111111111",
                    registrationAddress = "Kayitli kart ile odeme",
                    city = "Istanbul",
                    country = "Turkey",
                    ip = "85.34.78.112"
                },
                basketItems = new[]
                {
                    new { id = "BI-1", name = "Sepet", category1 = "Genel", price = input.Price }
                }
            };

            using var resp = await http.PostAsJsonAsync(url, body, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Çekim başarısız: {Status}", resp.StatusCode);
                return null;
            }

            using var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var root = json.RootElement;
            return new GatewayChargeResult(
                root.GetProperty("paymentId").GetGuid(),
                root.GetProperty("providerPaymentId").GetString() ?? string.Empty,
                root.GetProperty("status").GetString() ?? string.Empty,
                root.GetProperty("price").GetDecimal(),
                root.GetProperty("paidPrice").GetDecimal(),
                root.GetProperty("installment").GetInt32());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Çekim hata.");
            return null;
        }
    }

    private async Task<HttpClient> AuthorizedClientAsync(MerchantInformation merchant, CancellationToken ct)
    {
        var token = await _tokenProvider.GetTokenAsync(merchant.MerchantId, merchant.MerchantKey, ct);
        var http = _httpClientFactory.CreateClient("dropshop-vault");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return http;
    }
}
