namespace Payment.Api.Infrastructure;

// 077: Payment.Api → Customer.Api merchant API key istemcisi (PG hosted-payment X-Api-Key kaynağı).
// Customer /internal/merchant-key ucunu makine token'ıyla (customer.read; PaymentTokenHandler) çağırır.
// first-party mağaza = tek merchant → merchantId taşınmaz. Statik config yerine tek kaynak
// MerchantInformation (reset/rotate senkron derdi biter). Fail-closed: NotFound/erişilemez → null
// (link üretilmez). Key ASLA UI/LLM'e sızmaz.
public sealed class MerchantKeyClient(HttpClient http)
{
    private sealed record MerchantKeyReply(Guid merchantId, string merchantKey);

    public async Task<string?> GetKeyAsync(CancellationToken ct)
    {
        try
        {
            using var response = await http.GetAsync("api/v1/internal/merchant-key", ct);
            if (!response.IsSuccessStatusCode)
                return null;

            var reply = await response.Content.ReadFromJsonAsync<MerchantKeyReply>(cancellationToken: ct);
            return string.IsNullOrWhiteSpace(reply?.merchantKey) ? null : reply.merchantKey;
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
