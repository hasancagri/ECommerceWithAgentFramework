namespace Order.Api.Domains.PaymentAttempts;

// 039: durable reconcile mesaji (Order BC ici, durable local queue). PaymentAttempt.Id anahtar.
public record ReconcileTick(string CorrelationKey);

// 039 (US4): belirsiz cekimi durable, SINIRLI reconcile eder. Iki tetik tek idempotent yola iner:
// (1) OnChargeResult(ambiguous) -> ScheduleAsync(ReconcileTick); (2) kullanici chat'te tekrar sorunca
// place_order var-olan Unknown attempt icin HEMEN ReconcileTick yayinlar. Her tick PG retrieve(key) ->
// OnReconcileTick karari -> basari: siparis olustur; pending: backoff'la yeniden zamanla; deadline:
// terminal (NeedsReconciliation, ops gorunurluk). Asla cift cekim (retrieve key ile), asla sonsuz (deadline).
public class PaymentReconcileHandler
{
    [Transactional]
    public async Task Handle(
        ReconcileTick message,
        IDocumentSession session,
        IMessageBus bus,
        MerchantKeyClient merchantKey,
        PaymentGatewayClient gateway,
        CheckoutReconcile cfg,
        ILogger<PaymentReconcileHandler> logger,
        CancellationToken ct)
    {
        var attempt = await session.Query<PaymentAttempt>()
            .FirstOrDefaultAsync(x => x.Id == message.CorrelationKey, ct);
        if (attempt is null || attempt.IsTerminal)
            return; // bulunamadi / zaten terminal -> no-op (bayat/gec tick)

        // 049: PG X-Api-Key MerchantInformation'dan cozulur. Key erisilemezse retrieve'i Ambiguous say ->
        // OnReconcileTick normal backoff/deadline yolunu isletir (sonsuz degil, cift cekim yok).
        var apiKey = await merchantKey.GetKeyAsync(attempt.MerchantId, ct);
        var retrieve = apiKey is null
            ? RetrieveResult.Unknown
            : await gateway.RetrieveAsync(attempt.Id, attempt.MerchantId, apiKey, ct);
        var now = DateTimeOffset.UtcNow;
        var decision = attempt.OnReconcileTick(retrieve.Outcome, retrieve.PaymentId, retrieve.Price, now, cfg);

        switch (decision.Action)
        {
            case PaymentAttemptAction.CreateOrder:
                var (orderId, code) = await PaymentOrderCreator.CreateAsync(attempt, session, bus, ct);
                attempt.OrderId = orderId;
                attempt.OrderCode = code;
                logger.LogInformation("Reconcile {Key}: gecikmeli basari -> siparis {Code} olusturuldu.", attempt.Id, code);
                break;

            case PaymentAttemptAction.ScheduleReconcile:
                await bus.ScheduleAsync(new ReconcileTick(attempt.Id), decision.ReconcileDelay!.Value);
                break;

            case PaymentAttemptAction.Terminal:
                logger.LogError(
                    "Reconcile {Key}: deadline doldu -> NeedsReconciliation (MANUEL mudahale; kullanici {UserId}, tutar {Amount}).",
                    attempt.Id, attempt.UserId, attempt.Amount);
                break;

            case PaymentAttemptAction.NotifyFailed:
            case PaymentAttemptAction.VerifyFailed:
                logger.LogWarning("Reconcile {Key}: PG kesin basarisiz/dogrulanamadi -> siparis yok.", attempt.Id);
                break;
        }

        session.Store(attempt);
    }
}
