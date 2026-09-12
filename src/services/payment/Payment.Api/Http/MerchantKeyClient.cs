namespace Payment.Api.Http;

// 075: Payment -> Customer merchant API key istemcisi (PG charge X-Api-Key kaynağı). Customer.Api
// /internal/merchant-key ucunu makine token'iyla (customer.read; SagaTokenHandler) çağırır. Tek kaynak
// MerchantInformation → reset/rotate senkron derdi yok. Fail-closed: NotFound/erişilemez → null
// (çekim yapılmaz). Key ASLA UI/LLM'e sızmaz.
public sealed class MerchantKeyClient(HttpClient http)
{
    private sealed record MerchantKeyReply(Guid merchantId, string merchantKey);

    public async Task<string?> GetKeyAsync(Guid merchantId, CancellationToken ct)
    {
        try
        {
            using var response = await http.GetAsync($"api/v1/internal/merchant-key?merchantId={merchantId}", ct);
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
