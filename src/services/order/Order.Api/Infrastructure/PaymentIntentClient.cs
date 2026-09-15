namespace Order.Api.Infrastructure;

// 077: Order.Api → Payment.Api hosted-CF ödeme girişimi istemcisi (senkron S2S; makine token payment.write,
// SagaTokenHandler). İki uç: canlı-intent sorgusu (re-use — order oluşturmadan önce) + link isteği.
// Fail-closed: erişilemez/hata → null (start_payment dostça Result hatası döner). Kontrat: contracts/store-internal.md.
public sealed class PaymentIntentClient(HttpClient http)
{
    public sealed record LiveIntent(Guid PaymentIntentId, Guid OrderId, string HostedUrl);
    public sealed record CreatedIntent(Guid PaymentIntentId, string HostedUrl);

    private sealed record LiveIntentReplyDto(Guid PaymentIntentId, Guid OrderId, string HostedUrl);
    private sealed record CreateIntentBody(Guid OrderId, Guid UserId, string BasketRef, decimal Amount, string TxRef);
    private sealed record CreateIntentReplyDto(Guid PaymentIntentId, string HostedUrl, bool Reused);

    // Canlı Pending intent var mı (aynı kullanıcı+sepet)? Yoksa null (404).
    public async Task<LiveIntent?> GetLiveAsync(Guid userId, string basketRef, CancellationToken ct)
    {
        try
        {
            var url = $"api/v1/internal/payments/intents/live?userId={userId}&basketRef={Uri.EscapeDataString(basketRef)}";
            using var response = await http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return null;

            var dto = await response.Content.ReadFromJsonAsync<LiveIntentReplyDto>(cancellationToken: ct);
            return dto is null ? null : new LiveIntent(dto.PaymentIntentId, dto.OrderId, dto.HostedUrl);
        }
        catch (HttpRequestException) { return null; }
        catch (TaskCanceledException) { return null; }
    }

    // Link iste: order + txRef verilir → hosted URL döner. Başarısız → null.
    public async Task<CreatedIntent?> CreateAsync(
        Guid orderId, Guid userId, string basketRef, decimal amount, string txRef, CancellationToken ct)
    {
        try
        {
            using var response = await http.PostAsJsonAsync("api/v1/internal/payments/intents",
                new CreateIntentBody(orderId, userId, basketRef, amount, txRef), ct);
            if (!response.IsSuccessStatusCode)
                return null;

            var dto = await response.Content.ReadFromJsonAsync<CreateIntentReplyDto>(cancellationToken: ct);
            return dto is null || string.IsNullOrWhiteSpace(dto.HostedUrl)
                ? null
                : new CreatedIntent(dto.PaymentIntentId, dto.HostedUrl);
        }
        catch (HttpRequestException) { return null; }
        catch (TaskCanceledException) { return null; }
    }
}
