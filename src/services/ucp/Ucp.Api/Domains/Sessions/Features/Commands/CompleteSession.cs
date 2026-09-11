using Ucp.Api.Grpc;

namespace Ucp.Api.Domains.Sessions.Features.Commands;

/// <summary>
/// Session'ı tamamlar (POST /ucp/checkout_sessions/{id}/complete). Ready + süre guard + idempotency
/// (BeginComplete); toplam tazedir (her mutasyonda hesaplanır); Order'a gRPC already-captured devri
/// (charge Order içinde/iyzico sandbox). Başarı → completed + order_ref; ödeme başarısız → completed
/// OLMAZ, session ready'ye geri sarılır + PAYMENT_FAILED messages, sipariş yok (FR-006/009).
/// external_ref=SessionId Order tarafında deterministik OrderId → tekrar complete yeni sipariş üretmez.
/// </summary>
public static class CompleteSession
{
    public record CompleteSessionCommand(string SessionId, string IdempotencyKey);

    [Transactional]
    public class CompleteSessionHandler
    {
        public async Task<FeatureObjectResultModel<SessionResult>> Handle(
            CompleteSessionCommand cmd, IDocumentSession session, ExternalOrderClient externalOrder, CancellationToken ct)
        {
            var agg = await SessionStore.LoadAsync(session, cmd.SessionId, ct);
            if (agg is null) return FeatureObjectResultModel<SessionResult>.NotFound();

            var begin = agg.BeginComplete(cmd.IdempotencyKey, DateTimeOffset.UtcNow);
            if (!begin.IsSuccess) return FeatureObjectResultModel<SessionResult>.Error(begin.Messages);

            // Idempotent tekrar: zaten tamamlanmışsa mevcut sonucu döner (yeni sipariş yok).
            if (agg.Status == UcpSessionStatus.Completed)
                return FeatureObjectResultModel<SessionResult>.Ok(SessionResult.From(agg));

            var outcome = await externalOrder.CreateAsync(agg, ct);
            if (!outcome.Charged || string.IsNullOrWhiteSpace(outcome.OrderRef))
            {
                agg.FailComplete();
                session.Update(agg);
                return FeatureObjectResultModel<SessionResult>.Error(new List<MessageItem>
                {
                    new() { Code = UcpResourceConstants.PAYMENT_FAILED, Params = { outcome.Message } }
                });
            }

            var completed = agg.MarkCompleted(outcome.OrderRef);
            if (!completed.IsSuccess) return FeatureObjectResultModel<SessionResult>.Error(completed.Messages);

            session.Update(agg);
            return FeatureObjectResultModel<SessionResult>.Ok(SessionResult.From(agg));
        }
    }
}