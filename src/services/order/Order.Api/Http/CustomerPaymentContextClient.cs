namespace Order.Api.Http;

// 039: Customer yapisal odeme-baglami yaniti (PaymentContextView Order-tarafi karsiligi, 14 alan).
// Found=false ise adres/kart/merchant yok VEYA Customer erisilemez -> place_order reddedilir (FR-009).
public sealed record PaymentContext(
    Guid MerchantId,
    string VaultToken,
    string CardBrand,
    string CardLast4,
    bool CardIsDefault,
    string BuyerName,
    string BuyerSurname,
    string BuyerEmail,
    string BuyerGsmNumber,
    string BuyerIdentityNumber,
    string BuyerRegistrationAddress,
    string BuyerCity,
    string BuyerCountry,
    string BuyerIp);

// 039: Order -> Customer yapisal odeme-baglami istemcisi. Customer.Api /internal/payment-context
// ucunu makine token'iyla (customer.read; SagaTokenHandler) cagirir. Fail-closed: NotFound veya
// erisilemez -> null (siparis olusmaz). VaultToken/merchantId asla UI/LLM'e sizmaz; yalniz PG charge.
public sealed class CustomerPaymentContextClient(HttpClient http)
{
    public async Task<PaymentContext?> GetAsync(Guid userId, Guid? cardId, CancellationToken ct)
    {
        try
        {
            var url = $"api/v1/internal/payment-context?userId={userId}";
            if (cardId is { } id) url += $"&cardId={id}";

            using var response = await http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return null; // NotFound (adres/kart/merchant yok) veya yetki/hata -> reddet

            return await response.Content.ReadFromJsonAsync<PaymentContext>(cancellationToken: ct);
        }
        catch (HttpRequestException)
        {
            return null; // fail-closed: Customer erisilemez
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }
}
