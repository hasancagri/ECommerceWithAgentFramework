using System.Net.Http.Json;

namespace Order.Api.Http;

// 049: Order -> Customer merchant API key istemcisi (PG charge/retrieve X-Api-Key kaynagi). Customer.Api
// /internal/merchant-key ucunu makine token'iyla (customer.read; SagaTokenHandler) cagirir. Statik config
// anahtari yerine tek kaynak MerchantInformation -> reset/rotate senkron derdi biter. Fail-closed:
// NotFound/erisilemez -> null (cekim yapilmaz / reconcile belirsiz sayar). Key ASLA UI/LLM'e sizmaz.
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
            return null; // fail-closed: Customer erisilemez
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }
}
