using static Shared.CheckoutMessages;
using PaymentAggregate = Payment.Api.Domains.Payments.Payment;

namespace Payment.Api;

// 049: checkout orchestrator iki-faz ödeme broker handler'ları. Komutları PaymentCommandsQueue'dan
// tüketir, Payment aggregate davranışını (Authorize/Capture/Void — İlke II) tetikler, sonucu reply
// kuyruğuna cascading message ile yayınlar. PSP hop stub (FR-015). Idempotent: aynı checkoutId tek Authorize.
public class PaymentEventHandlers
{
    [Transactional]
    public async Task<PaymentAuthorized> Handle(AuthorizePaymentCommand cmd, IDocumentSession session, CancellationToken ct)
    {
        var existing = await session.Query<PaymentAggregate>().FirstOrDefaultAsync(p => p.CheckoutId == cmd.CheckoutId, ct);
        if (existing is not null)
            return new PaymentAuthorized(cmd.CheckoutId, existing.Id, existing.AuthorizationRef ?? "", true, ErrorClass.None);

        var result = PaymentAggregate.Authorize(cmd.UserId, cmd.Amount, cmd.CheckoutId);
        if (!result.IsSuccess)
            return new PaymentAuthorized(cmd.CheckoutId, Guid.Empty, "", false, ErrorClass.Permanent,
                result.Messages.FirstOrDefault()?.Code);

        session.Store(result.Data!);
        return new PaymentAuthorized(cmd.CheckoutId, result.Data!.Id, result.Data!.AuthorizationRef ?? "", true, ErrorClass.None);
    }

    [Transactional]
    public async Task<PaymentCaptured> Handle(CapturePaymentCommand cmd, IDocumentSession session, CancellationToken ct)
    {
        var payment = await session.LoadAsync<PaymentAggregate>(cmd.PaymentId, ct);
        if (payment is null)
            return new PaymentCaptured(cmd.CheckoutId, false, ErrorClass.Permanent, PaymentResourceConstants.PAYMENT_INVALID_TRANSITION);

        var result = payment.Capture();
        if (!result.IsSuccess)
            return new PaymentCaptured(cmd.CheckoutId, false, ErrorClass.Permanent, result.Messages.FirstOrDefault()?.Code);

        session.Store(payment);
        return new PaymentCaptured(cmd.CheckoutId, true, ErrorClass.None);
    }

    [Transactional]
    public async Task<PaymentVoided> Handle(VoidPaymentCommand cmd, IDocumentSession session, CancellationToken ct)
    {
        var payment = await session.LoadAsync<PaymentAggregate>(cmd.PaymentId, ct);
        if (payment is null)
            return new PaymentVoided(cmd.CheckoutId, true, ErrorClass.None); // yok = zaten void sayılır (idempotent)

        var result = payment.Void();
        if (!result.IsSuccess)
            return new PaymentVoided(cmd.CheckoutId, false, ErrorClass.Permanent, result.Messages.FirstOrDefault()?.Code);

        session.Store(payment);
        return new PaymentVoided(cmd.CheckoutId, true, ErrorClass.None);
    }
}