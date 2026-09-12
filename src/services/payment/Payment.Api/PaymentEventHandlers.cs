using Payment.Api.Http;
using static Shared.CheckoutMessages;
using PaymentAggregate = Payment.Api.Domains.Payments.Payment;

namespace Payment.Api;

// 075: checkout orchestrator tek-faz ödeme broker handler'ı. ChargePaymentCommand'ı PaymentCommandsQueue'dan
// tüketir → Customer payment-context S2S (UserId+CardHandle → PgUserHandle+CardHandle+buyer+merchantId) +
// merchant API key → PG NON-3D çekim (handle'larla; PAN yok, 3DS yok) → PaymentCharged reply. Çekim
// sahibi Payment BC (İlke I; analyze I1). Idempotent: aynı checkoutId tek ödeme (var olan → aynı PaymentId).
// Belirsiz (Ambiguous) sonuç → başarısız değil geçici sayılır (Transient) → Wolverine yeniden dener.
public class PaymentEventHandlers(
    CustomerPaymentContextClient customer,
    MerchantKeyClient merchantKey,
    PaymentGatewayClient gateway)
{
    [Transactional]
    public async Task<PaymentCharged> Handle(ChargePaymentCommand cmd, IDocumentSession session, CancellationToken ct)
    {
        // Idempotent: aynı checkout için ödeme varsa yeniden çekme.
        var existing = await session.Query<PaymentAggregate>().FirstOrDefaultAsync(p => p.CheckoutId == cmd.CheckoutId, ct);
        if (existing is not null)
            return new PaymentCharged(cmd.CheckoutId, existing.Id, true, ErrorClass.None);

        // Ödeme bağlamı — kullanıcının PG kart-handle'ları + buyer + merchantId (S2S). Yoksa kalıcı hata.
        var ctx = await customer.GetAsync(cmd.UserId, cmd.CardHandle, ct);
        if (ctx is null)
            return new PaymentCharged(cmd.CheckoutId, Guid.Empty, false, ErrorClass.Permanent,
                PaymentResourceConstants.PAYMENT_CONTEXT_MISSING);

        // PG X-Api-Key (MerchantInformation tek kaynak). Yoksa kalıcı hata (onboarding eksik).
        var apiKey = await merchantKey.GetKeyAsync(ctx.MerchantId, ct);
        if (apiKey is null)
            return new PaymentCharged(cmd.CheckoutId, Guid.Empty, false, ErrorClass.Permanent,
                PaymentResourceConstants.PAYMENT_MERCHANT_KEY_MISSING);

        // PG NON-3D çekim (idempotency çapası = checkoutId). Ambiguous → geçici (retry); Failed → kalıcı.
        var charge = await gateway.ChargeAsync(cmd.CheckoutId.ToString(), ctx.MerchantId, apiKey, ctx, cmd.Amount, ct);
        if (charge.Outcome == PaymentOutcome.Ambiguous)
            return new PaymentCharged(cmd.CheckoutId, Guid.Empty, false, ErrorClass.Transient,
                PaymentResourceConstants.PAYMENT_CHARGE_AMBIGUOUS);
        if (charge.Outcome == PaymentOutcome.Failed)
            return new PaymentCharged(cmd.CheckoutId, Guid.Empty, false, ErrorClass.Permanent,
                PaymentResourceConstants.PAYMENT_CHARGE_FAILED);

        var result = PaymentAggregate.Charge(cmd.UserId, cmd.Amount, cmd.CheckoutId, charge.PgPaymentId ?? "");
        if (!result.IsSuccess)
            return new PaymentCharged(cmd.CheckoutId, Guid.Empty, false, ErrorClass.Permanent,
                result.Messages.FirstOrDefault()?.Code);

        session.Store(result.Data!);
        return new PaymentCharged(cmd.CheckoutId, result.Data!.Id, true, ErrorClass.None);
    }
}
