namespace Payment.Api.Domains.Payments.Features.Commands;

// 077 US1: hosted ödeme linki üret. Order.Api S2S ile çağırır. Akış: canlı-intent re-use → yoksa
// MerchantKey al → PG hosted-payment çağır → PaymentIntent(Pending) sakla → terk-timer kur → HostedUrl dön.
// [Transactional]: intent yazımı + schedule aynı commit'te. Re-use (Q1/A2): aynı UserId+BasketRef için
// canlı Pending varsa mevcut HostedUrl döner (yeni PG çağrısı/kayıt yok) — nadir yarışta order orphan olur
// (Pending, stok değmez; kabul).
public static class CreatePaymentIntent
{
    public record CreatePaymentIntentCommand(
        Guid OrderId, Guid UserId, string BasketRef, decimal Amount, string TxRef, string CallbackUrl);

    public class CreatePaymentIntentResponse
    {
        public Guid PaymentIntentId { get; set; }
        public string HostedUrl { get; set; } = string.Empty;
        public bool Reused { get; set; }
    }

    public class CreatePaymentIntentCommandHandler(
        IDocumentSession session,
        IMessageBus bus,
        MerchantKeyClient merchantKey,
        PgHostedPaymentClient gateway,
        PaymentOptions options)
    {
        [Transactional]
        public async Task<FeatureObjectResultModel<CreatePaymentIntentResponse>> Handle(
            CreatePaymentIntentCommand cmd, CancellationToken ct)
        {
            if (cmd.Amount <= 0)
                return FeatureObjectResultModel<CreatePaymentIntentResponse>.Error(
                    new MessageItem { Code = PaymentResourceConstants.PAYMENT_AMOUNT_INVALID });

            // Re-use: aynı kullanıcı+sepet için canlı Pending intent varsa mevcut linki döndür.
            var candidates = await session.Query<PaymentIntent>()
                .Where(p => p.UserId == cmd.UserId && p.BasketRef == cmd.BasketRef
                            && p.Status == PaymentIntentStatus.Pending)
                .ToListAsync(ct);
            var live = candidates.FirstOrDefault(p => p.IsLive(options.IntentTimeoutSeconds, DateTime.UtcNow));
            if (live is not null)
                return FeatureObjectResultModel<CreatePaymentIntentResponse>.Ok(
                    new CreatePaymentIntentResponse { PaymentIntentId = live.Id, HostedUrl = live.HostedUrl, Reused = true });

            // MerchantKey (PG X-Api-Key) — tek kaynak Customer MerchantInformation. Yoksa fail-closed.
            var apiKey = await merchantKey.GetKeyAsync(ct);
            if (apiKey is null)
                return FeatureObjectResultModel<CreatePaymentIntentResponse>.Error(
                    new MessageItem { Code = PaymentResourceConstants.PAYMENT_MERCHANT_KEY_UNAVAILABLE });

            // PG hosted-payment — dönen hosted URL + PG referansı. Erişilemez/hata → fail-closed.
            var pg = await gateway.StartHostedPaymentAsync(cmd.Amount, cmd.TxRef, cmd.CallbackUrl, apiKey, ct);
            if (!pg.Success)
                return FeatureObjectResultModel<CreatePaymentIntentResponse>.Error(
                    new MessageItem { Code = PaymentResourceConstants.PAYMENT_GATEWAY_UNAVAILABLE });

            var create = PaymentIntent.Create(
                cmd.OrderId, cmd.UserId, cmd.BasketRef, cmd.Amount, cmd.TxRef, pg.PgPaymentRef, pg.HostedUrl!);
            if (!create.IsSuccess)
                return FeatureObjectResultModel<CreatePaymentIntentResponse>.Error(create.Messages);

            var intent = create.Data!;
            session.Store(intent);

            // Terk-timer: timeout sonunda hâlâ Pending ise Expire + PaymentFailed("ABANDONED"). Marten-backed.
            await bus.ScheduleAsync(new Payment.Api.Process.PaymentIntentExpiryCheck(intent.TxRef),
                TimeSpan.FromSeconds(options.IntentTimeoutSeconds));

            return FeatureObjectResultModel<CreatePaymentIntentResponse>.Ok(
                new CreatePaymentIntentResponse { PaymentIntentId = intent.Id, HostedUrl = intent.HostedUrl, Reused = false });
        }
    }
}
