using static Shared.CheckoutMessages;
using PaymentAggregate = Payment.Api.Domains.Payments.Payment;

namespace Payment.Api;

// 049: checkout orchestrator tek-faz ödeme broker handler'ı. ChargePaymentCommand'ı PaymentCommandsQueue'dan
// tüketir, Payment.Charge davranışını (İlke II) tetikler, PaymentCharged'i reply kuyruğuna cascading message
// ile yayınlar. PSP hop stub (FR-015). Idempotent: aynı checkoutId tek ödeme (var olan → aynı PaymentId).
// Void/capture handler'ları söküldü — ödeme saga'nın son pivot adımı, geri-alma yok.
public class PaymentEventHandlers
{
    [Transactional]
    public async Task<PaymentCharged> Handle(ChargePaymentCommand cmd, IDocumentSession session, CancellationToken ct)
    {
        var existing = await session.Query<PaymentAggregate>().FirstOrDefaultAsync(p => p.CheckoutId == cmd.CheckoutId, ct);
        if (existing is not null)
            return new PaymentCharged(cmd.CheckoutId, existing.Id, true, ErrorClass.None);

        var result = PaymentAggregate.Charge(cmd.UserId, cmd.Amount, cmd.CheckoutId);
        if (!result.IsSuccess)
            return new PaymentCharged(cmd.CheckoutId, Guid.Empty, false, ErrorClass.Permanent,
                result.Messages.FirstOrDefault()?.Code);

        session.Store(result.Data!);
        return new PaymentCharged(cmd.CheckoutId, result.Data!.Id, true, ErrorClass.None);
    }
}
