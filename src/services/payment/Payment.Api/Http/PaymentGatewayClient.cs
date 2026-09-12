namespace Payment.Api.Http;

// 075: PG çekim sonucu. Ambiguous = pending VEYA erişilemez/timeout → kesin değil (asla kesin "başarılı"
// deme). Success/Failed kesin durumlar.
public enum PaymentOutcome { Success, Failed, Ambiguous }

public sealed record ChargeResult(PaymentOutcome Outcome, string? PgPaymentId)
{
    public static readonly ChargeResult Unknown = new(PaymentOutcome.Ambiguous, null);
}

// 075: Payment -> PaymentGateway (dış repo) NON-3D çekim S2S REST. Auth: merchant API key (X-Api-Key;
// kullanıcı JWT değil). vaultToken KALKTI → userHandle + cardHandle (kart PG'de). installment
// KALKTI (tek çekim — Google-Pay-like). 3DS YOK (NON-3D; onay agent konuşmasında — FR-014). sağlayıcı PG'nin
// İÇİNDE (FR-016); mağaza yalnız bu sözleşmeyi bilir (contracts/pg-card-contract.md).
public sealed class PaymentGatewayClient(HttpClient http)
{
    public const string ApiKeyHeader = "X-Api-Key";

    private sealed record ChargeRequest(
        string correlationKey, string userHandle, string cardHandle, decimal price, decimal paidPrice,
        string currency, BuyerPayload buyer);

    private sealed record BuyerPayload(
        string name, string surname, string email, string gsmNumber, string identityNumber,
        string registrationAddress, string city, string country, string ip);

    private sealed record PaymentReply(string? paymentId, string? status, decimal price, decimal paidPrice);

    public async Task<ChargeResult> ChargeAsync(
        string correlationKey, Guid merchantId, string apiKey, PaymentContext ctx, decimal amount,
        CancellationToken ct)
    {
        var body = new ChargeRequest(
            correlationKey, ctx.PgUserHandle, ctx.CardHandle, amount, amount, "TRY",
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
                return ChargeResult.Unknown; // HTTP hatası (401/timeout dahil) → belirsiz

            var reply = await response.Content.ReadFromJsonAsync<PaymentReply>(cancellationToken: ct);
            return Map(reply);
        }
        catch (HttpRequestException)
        {
            return ChargeResult.Unknown; // çekim gitmiş OLABİLİR → belirsiz
        }
        catch (TaskCanceledException)
        {
            return ChargeResult.Unknown;
        }
    }

    private static ChargeResult Map(PaymentReply? reply)
    {
        if (reply is null) return ChargeResult.Unknown;
        return reply.status switch
        {
            "success" => new ChargeResult(PaymentOutcome.Success, reply.paymentId),
            "failed" => new ChargeResult(PaymentOutcome.Failed, reply.paymentId),
            _ => new ChargeResult(PaymentOutcome.Ambiguous, reply.paymentId) // pending / bilinmeyen
        };
    }
}
