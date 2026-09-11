using System.Text;
using Ucp.Api.Signatures;

namespace Ucp.Api.Webhooks;

/// <summary>
/// 072 US3: sipariş olayını platform inbox'ına imzalı (RFC 9421) POST eder + sınırlı retry/backoff
/// (≤4 deneme; SC-004: ≤3 denemede teslim). Teslim durumu OutboundDelivery izine yazılır (çağıran store eder).
/// UCP-Agent header = mağaza profil URL'i. İmza = mağaza private key (HttpMessageSigner).
/// </summary>
public sealed class UcpWebhookSender(
    IHttpClientFactory factory,
    HttpMessageSigner signer,
    UcpPlatformOption platform,
    ILogger<UcpWebhookSender> logger) : ISingletonDependency
{
    private static readonly TimeSpan[] Backoff = [TimeSpan.Zero, TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(800), TimeSpan.FromSeconds(2)];

    public async Task<OutboundDelivery> SendAsync(
        string eventType, string orderRef, string sessionId, DateTimeOffset occurredAt, CancellationToken ct)
    {
        var target = platform.WebhookInboxUrl;
        var delivery = new OutboundDelivery
        {
            Id = Guid.NewGuid(),
            OrderRef = orderRef,
            EventType = eventType,
            TargetUrl = target,
            CreatedAt = occurredAt
        };

        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            type = eventType,
            order_ref = orderRef,
            session_id = sessionId,
            occurred_at = occurredAt.ToString("O")
        });
        var body = Encoding.UTF8.GetBytes(payload);

        var client = factory.CreateClient("ucp-webhook");

        for (var attempt = 0; attempt < Backoff.Length; attempt++)
        {
            if (Backoff[attempt] > TimeSpan.Zero) await Task.Delay(Backoff[attempt], ct);
            delivery.Attempts = attempt + 1;

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, target)
                {
                    Content = new ByteArrayContent(body)
                };
                req.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

                var headers = signer.Sign("POST", target, body, occurredAt.ToUnixTimeSeconds());
                req.Headers.TryAddWithoutValidation("Content-Digest", headers.ContentDigest);
                req.Headers.TryAddWithoutValidation("Signature-Input", headers.SignatureInput);
                req.Headers.TryAddWithoutValidation("Signature", headers.Signature);
                req.Headers.TryAddWithoutValidation("UCP-Agent", platform.StoreProfileUrl);

                using var res = await client.SendAsync(req, ct);
                if (res.IsSuccessStatusCode)
                {
                    delivery.Delivered = true;
                    delivery.LastError = null;
                    return delivery;
                }
                delivery.LastError = $"HTTP {(int)res.StatusCode}";
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                delivery.LastError = ex.Message;
            }
        }

        logger.LogWarning("UCP webhook teslim edilemedi ({Event} {OrderRef}) {Attempts} deneme: {Error}",
            eventType, orderRef, delivery.Attempts, delivery.LastError);
        return delivery;
    }
}
