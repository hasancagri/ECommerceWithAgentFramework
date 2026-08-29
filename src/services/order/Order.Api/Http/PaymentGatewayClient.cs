using System.Net.Http.Json;

namespace Order.Api.Http;

// 039: PG cekim/retrieve sonucu. Ambiguous = pending VEYA erisilemez/timeout -> kesin degil, reconcile
// gerekir (asla kesin "basarisiz" deme). Success/Failed kesin durumlardir.
public enum PaymentOutcome { Success, Failed, Ambiguous }

public sealed record ChargeResult(PaymentOutcome Outcome, string? PaymentId, decimal Price)
{
    public static readonly ChargeResult Unknown = new(PaymentOutcome.Ambiguous, null, 0m);
}

public sealed record RetrieveResult(PaymentOutcome Outcome, string? PaymentId, decimal Price)
{
    public static readonly RetrieveResult Unknown = new(PaymentOutcome.Ambiguous, null, 0m);
}

// 039: Order -> PaymentGateway (dis repo) server-to-server REST. Auth: merchant API key (kullanici
// JWT degil). Charge idempotent (correlationKey); retrieve verify + reconcile icin. Yanit kaybolursa
// (timeout/kesinti) Ambiguous doner -> saga reconcile eder (asla cift cekim: ayni key -> var olan odeme).
// 049: X-Api-Key PER-REQUEST verilir (statik default header degil) — key artik MerchantInformation'dan
// (MerchantKeyClient) cozulur; caller merchantId'ye ait key'i gecer.
public sealed class PaymentGatewayClient(HttpClient http)
{
    public const string ApiKeyHeader = "X-Api-Key";

    private sealed record ChargeRequest(
        string correlationKey, string vaultToken, decimal price, decimal paidPrice,
        string currency, int installment, BuyerPayload buyer);

    private sealed record BuyerPayload(
        string name, string surname, string email, string gsmNumber, string identityNumber,
        string registrationAddress, string city, string country, string ip);

    private sealed record PaymentReply(string? paymentId, string? status, decimal price, decimal paidPrice);

    public async Task<ChargeResult> ChargeAsync(
        string correlationKey, Guid merchantId, string apiKey, PaymentContext ctx, decimal amount, int installment,
        CancellationToken ct)
    {
        var body = new ChargeRequest(
            correlationKey, ctx.VaultToken, amount, amount, "TRY", installment,
            new BuyerPayload(ctx.BuyerName, ctx.BuyerSurname, ctx.BuyerEmail, ctx.BuyerGsmNumber,
                ctx.BuyerIdentityNumber, ctx.BuyerRegistrationAddress, ctx.BuyerCity, ctx.BuyerCountry, ctx.BuyerIp));

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"merchants/{merchantId}/payments")
            {
                Content = JsonContent.Create(body)
            };
            request.Headers.Add(ApiKeyHeader, apiKey);

            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                return ChargeResult.Unknown; // HTTP hatasi (401/timeout dahil) -> belirsiz (reconcile)

            var reply = await response.Content.ReadFromJsonAsync<PaymentReply>(cancellationToken: ct);
            return Map(reply) is var (outcome, id, price) ? new ChargeResult(outcome, id, price) : ChargeResult.Unknown;
        }
        catch (HttpRequestException)
        {
            return ChargeResult.Unknown; // cekim gitmis OLABILIR -> reconcile
        }
        catch (TaskCanceledException)
        {
            return ChargeResult.Unknown;
        }
    }

    public async Task<RetrieveResult> RetrieveAsync(
        string correlationKey, Guid merchantId, string apiKey, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get, $"merchants/{merchantId}/payments?correlationKey={correlationKey}");
            request.Headers.Add(ApiKeyHeader, apiKey);

            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                return RetrieveResult.Unknown;

            var reply = await response.Content.ReadFromJsonAsync<PaymentReply>(cancellationToken: ct);
            return Map(reply) is var (outcome, id, price) ? new RetrieveResult(outcome, id, price) : RetrieveResult.Unknown;
        }
        catch (HttpRequestException)
        {
            return RetrieveResult.Unknown;
        }
        catch (TaskCanceledException)
        {
            return RetrieveResult.Unknown;
        }
    }

    private static (PaymentOutcome, string?, decimal) Map(PaymentReply? reply)
    {
        if (reply is null) return (PaymentOutcome.Ambiguous, null, 0m);
        return reply.status switch
        {
            "success" => (PaymentOutcome.Success, reply.paymentId, reply.price),
            "failed" => (PaymentOutcome.Failed, reply.paymentId, reply.price),
            _ => (PaymentOutcome.Ambiguous, reply.paymentId, reply.price) // pending / bilinmeyen
        };
    }
}
