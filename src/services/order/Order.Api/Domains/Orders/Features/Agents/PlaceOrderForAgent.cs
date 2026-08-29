namespace Order.Api.Domains.Orders.Features.Agents;

// 039 (Yol 2): chat'ten uctan uca siparis. LLM yalniz place_order'i secer + cardId?/installment verir;
// GERISI SUNUCU (LLM'siz): sepet kalemi (gRPC, sunucu-otoritesi) + buyer/adres/vaultToken (Customer
// yapisal) + correlation-key + PG cekim + siparis. Para/guven asla LLM'de. Agent slice IZOLE:
// Features/Commands'i IMessageBus ile CAGIRMAZ ([[agent-features-folder-convention]]); siparis olusturmayi
// PaymentOrderCreator ile dogrudan yapar (StartCheckout yayinlar). Cekim idempotent (correlation-key),
// siparis idempotent (paymentId). Belirsiz cekim durable reconcile'a devredilir (PaymentReconcile).
public static class PlaceOrderForAgent
{
    // order.write: MCP kullanici token'i tasir; Wolverine ScopeAuthorizationMiddleware zorlar.
    [RequiredScope(AuthorizationScopes.OrderWrite)]
    public record PlaceOrderCommand(Guid UserId, Guid? CardId, int Installment);

    public class PlaceOrderResponse
    {
        // created / payment_failed / pending / rejected
        public string Outcome { get; set; } = default!;
        public string? OrderCode { get; set; }
        public int ItemCount { get; set; }
        public decimal TotalPrice { get; set; }
        public string Message { get; set; } = default!;
    }

    [Transactional]
    public class PlaceOrderCommandHandler(
        IDocumentSession session,
        IMessageBus bus,
        BasketItemsClientProxy basket,
        CustomerPaymentContextClient customer,
        MerchantKeyClient merchantKey,
        PaymentGatewayClient gateway,
        CorrelationKeyOption keyOption,
        CheckoutReconcile reconcileCfg)
    {
        public async Task<FeatureObjectResultModel<PlaceOrderResponse>> Handle(
            PlaceOrderCommand cmd, CancellationToken ct)
        {
            var installment = cmd.Installment < 1 ? 1 : cmd.Installment;

            // 1) Sepet kalemleri — sunucu-otoritesi (gRPC). Fail-closed: erisilemez -> siparis yok (S5).
            var snapshot = await basket.GetItemsAsync(cmd.UserId, ct);
            if (!snapshot.Reachable)
                return Reject("Su an siparis alinamiyor, lutfen sonra tekrar dene.");
            if (snapshot.IsEmpty)
                return Reject("Sepetin bos, siparis olusturulamaz.");

            // 2) Odeme baglami — buyer + vaultToken + varsayilan adres (Customer yapisal). Yoksa red (FR-009).
            var ctx = await customer.GetAsync(cmd.UserId, cmd.CardId, ct);
            if (ctx is null)
                return Reject("Odeme icin kayitli kart veya varsayilan adres bulunamadi.");

            // 2b) Merchant API key — PG X-Api-Key (yapisal S2S; MerchantInformation tek kaynak). Yoksa red
            // (fail-closed: onboarding eksik/anahtar girilmemis -> cekim denenmez).
            var apiKey = await merchantKey.GetKeyAsync(ctx.MerchantId, ct);
            if (apiKey is null)
                return Reject("Odeme altyapisi anahtari bulunamadi. Lutfen sonra tekrar dene.");

            // 3) Correlation-key — deterministik + HMAC (sahiplik + idempotency capasi). Sunucu uretir.
            var key = CorrelationKey.Create(cmd.UserId, snapshot.ContentHash, installment, keyOption.ServerSecret).Value;

            // 4) Idempotent re-entry: ayni sepet+taksit icin var olan girisim varsa yeni cekim YOK.
            var existing = await session.LoadAsync<PaymentAttempt>(key, ct);
            if (existing is not null)
                return await ResumeExisting(existing, snapshot, ct);

            var amount = snapshot.TotalPrice;
            var address = new CreateOrder.AddressDto(
                Province: ctx.BuyerCity, District: "", Street: "", ZipCode: "", Line: ctx.BuyerRegistrationAddress);
            var attempt = PaymentAttempt.Begin(
                key, cmd.UserId, ctx.MerchantId, amount, installment, cmd.CardId,
                snapshot.Items, address, DateTimeOffset.UtcNow, reconcileCfg);

            // 5) PG cekim — idempotent (correlation-key). Yanit kaybolursa Ambiguous -> reconcile.
            var charge = await gateway.ChargeAsync(key, ctx.MerchantId, apiKey, ctx, amount, installment, ct);
            var decision = attempt.OnChargeResult(
                charge.Outcome, charge.PaymentId, charge.Price, DateTimeOffset.UtcNow, reconcileCfg);

            var response = await Apply(decision, attempt, snapshot, ct);
            session.Store(attempt);
            return response;
        }

        // Var olan girisimi ilerlet (idempotent). Succeeded -> ayni siparis; Unknown -> hemen tick (on-demand).
        private async Task<FeatureObjectResultModel<PlaceOrderResponse>> ResumeExisting(
            PaymentAttempt attempt, BasketSnapshot snapshot, CancellationToken ct)
        {
            switch (attempt.Status)
            {
                case PaymentAttemptStatus.Succeeded:
                    return Ok("created", $"Siparisin zaten olusturuldu. Siparis kodu: {attempt.OrderCode}.",
                        snapshot, attempt.OrderCode);

                case PaymentAttemptStatus.Failed:
                    return Ok("payment_failed", "Onceki odeme alinamadi. Farkli kartla tekrar deneyebilirsin.", snapshot);

                case PaymentAttemptStatus.NeedsReconciliation:
                    return Ok("pending", PendingMessage, snapshot);

                default: // Unknown / Charging — kullanici tekrar sordu: HEMEN bir reconcile tick koy (T039).
                    await bus.PublishAsync(new ReconcileTick(attempt.Id));
                    return Ok("pending", PendingMessage, snapshot);
            }
        }

        // Charge kararini uygular; siparis olusturma / reconcile zamanlama burada. Attempt cagiran store eder.
        private async Task<FeatureObjectResultModel<PlaceOrderResponse>> Apply(
            PaymentAttemptDecision decision, PaymentAttempt attempt, BasketSnapshot snapshot, CancellationToken ct)
        {
            switch (decision.Action)
            {
                case PaymentAttemptAction.CreateOrder:
                    var (orderId, code) = await PaymentOrderCreator.CreateAsync(attempt, session, bus, ct);
                    attempt.OrderId = orderId;
                    attempt.OrderCode = code;
                    return Ok("created", $"Siparisin olusturuldu. Siparis kodu: {code}.", snapshot, code);

                case PaymentAttemptAction.NotifyFailed:
                    return Ok("payment_failed", "Odeme alinamadi. Lutfen tekrar dene veya farkli kart sec.", snapshot);

                case PaymentAttemptAction.VerifyFailed:
                    return Ok("payment_failed", "Odeme dogrulanamadi, siparis olusturulmadi.", snapshot);

                case PaymentAttemptAction.ScheduleReconcile:
                    await bus.ScheduleAsync(new ReconcileTick(attempt.Id), decision.ReconcileDelay!.Value);
                    return Ok("pending", PendingMessage, snapshot);

                default: // AlreadyCompleted (charge sonrasi nadir) — var olan siparis
                    return Ok("created", $"Siparisin olusturuldu. Siparis kodu: {attempt.OrderCode}.",
                        snapshot, attempt.OrderCode);
            }
        }

        private const string PendingMessage =
            "Odemen alinmis olabilir, kontrol ediliyor. Durumu birazdan 'siparislerim' ile gorebilirsin.";

        private static FeatureObjectResultModel<PlaceOrderResponse> Ok(
            string outcome, string message, BasketSnapshot snapshot, string? code = null) =>
            FeatureObjectResultModel<PlaceOrderResponse>.Ok(new PlaceOrderResponse
            {
                Outcome = outcome,
                OrderCode = code,
                ItemCount = snapshot.Items.Count,
                TotalPrice = snapshot.TotalPrice,
                Message = message
            });

        private static FeatureObjectResultModel<PlaceOrderResponse> Reject(string message) =>
            FeatureObjectResultModel<PlaceOrderResponse>.Ok(new PlaceOrderResponse
            {
                Outcome = "rejected",
                ItemCount = 0,
                TotalPrice = 0,
                Message = message
            });
    }
}
