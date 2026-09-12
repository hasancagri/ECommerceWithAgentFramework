namespace Payment.Api.Http;

// 075: Customer yapısal ödeme-bağlamı yanıtı (PaymentContextView Payment-tarafı karşılığı). VaultToken
// KALKTI → PG kart-handle'ları (PgUserHandle + CardHandle) + buyer + MerchantId. Found=false ise kart/
// adres/merchant yok VEYA Customer erişilemez → çekim başarısız (fail-closed). Handle'lar yalnız PG
// çekiminde kullanılır; PAN/CVV asla.
public sealed record PaymentContext(
    Guid MerchantId,
    string PgUserHandle,
    string CardHandle,
    string BuyerName,
    string BuyerSurname,
    string BuyerEmail,
    string BuyerGsmNumber,
    string BuyerIdentityNumber,
    string BuyerRegistrationAddress,
    string BuyerCity,
    string BuyerCountry,
    string BuyerIp);

// 075: Payment -> Customer yapısal ödeme-bağlamı istemcisi. Customer.Api /internal/payment-context
// ucunu makine token'iyla (customer.read; SagaTokenHandler) çağırır. Fail-closed: NotFound/erişilemez
// → null (çekim yapılmaz). Handle'lar/merchantId asla UI/LLM'e sızmaz; yalnız PG charge.
public sealed class CustomerPaymentContextClient(HttpClient http)
{
    public async Task<PaymentContext?> GetAsync(Guid userId, string? cardHandle, CancellationToken ct)
    {
        try
        {
            var url = $"api/v1/internal/payment-context?userId={userId}";
            if (!string.IsNullOrWhiteSpace(cardHandle))
                url += $"&cardHandle={Uri.EscapeDataString(cardHandle)}";

            using var response = await http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return null; // NotFound (kart/adres/merchant yok) veya yetki/hata → çekim yok

            return await response.Content.ReadFromJsonAsync<PaymentContext>(cancellationToken: ct);
        }
        catch (HttpRequestException)
        {
            return null; // fail-closed: Customer erişilemez
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }
}
