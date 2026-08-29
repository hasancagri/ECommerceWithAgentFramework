namespace Order.Api.Domains.PaymentAttempts;

// 039: bir cekim girisiminin DAYANIKLILIK durumu (Marten belgesi; Id = CorrelationKey.Value).
//
// TASARIM NOTU: data-model bunu "Wolverine durable saga" olarak anar; burada bilincli olarak
// Wolverine `Saga` TABANI YERINE Marten-belgesi + saf karar metotlari + reconcile mesaj-handler'i
// kullanilir. Sebep: ilk cekim SENKRONDUR — place_order cagirana outcome (created/failed/pending)
// DONMELIDIR; Wolverine saga `Start` cagirana deger dondüremez (repo dersimiz: caching decorator ADR,
// "Before/After short-circuit'te deger dondüremez"). Dayaniklilik yine tam: durum Marten'da atomik
// persist edilir, belirsiz cekimler durable `ScheduleAsync(ReconcileTick)` ile cozulur. Karar mantigi
// saf On* metotlarindadir (birim testli, Ilke VI). Bkz. [[028-checkout-saga]], reconcile 026 deseni.
public class PaymentAttempt
{
    public string Id { get; set; } = default!; // = CorrelationKey.Value (hex HMAC)
    public Guid UserId { get; set; }
    public Guid MerchantId { get; set; } // PG retrieve/charge hedefi
    public int Installment { get; set; }
    public decimal Amount { get; set; }
    public string? ProviderPaymentId { get; set; } // PG saglayici kimligi (iz/referans)
    public PaymentAttemptStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset? NextCheckAt { get; set; }
    public DateTimeOffset DeadlineAt { get; set; }

    // Siparis blueprint'i: gecikmeli basari (reconcile) da ayni siparisi olusturabilsin diye tasinir.
    public Guid? OrderId { get; set; }
    public string? OrderCode { get; set; }
    public Guid? CardId { get; set; }
    public List<CreateOrder.OrderItemDto> Items { get; set; } = [];
    public CreateOrder.AddressDto Address { get; set; } = new("", "", "", "", "");

    [JsonIgnore]
    public bool IsTerminal =>
        Status is PaymentAttemptStatus.Succeeded or PaymentAttemptStatus.Failed
            or PaymentAttemptStatus.NeedsReconciliation;

    // Yeni girisim: Charging durumunda, siparis blueprint'iyle. Cekim henuz yapilmadi.
    public static PaymentAttempt Begin(
        string key, Guid userId, Guid merchantId, decimal amount, int installment, Guid? cardId,
        IReadOnlyList<CreateOrder.OrderItemDto> items, CreateOrder.AddressDto address, DateTimeOffset now,
        CheckoutReconcile cfg) => new()
    {
        Id = key,
        UserId = userId,
        MerchantId = merchantId,
        Amount = amount,
        Installment = installment,
        CardId = cardId,
        Items = [.. items],
        Address = address,
        Status = PaymentAttemptStatus.Charging,
        DeadlineAt = now.AddSeconds(cfg.DeadlineSeconds)
    };

    // --- saf karar cekirdegi (birim testlerin hedefi — mock'suz) ---

    // US1/US2: cekim sonucu -> gecis. Verify geidi: status success AND tutar == sepet toplami.
    // Sahiplik AYRI alan gerektirmez — cekim correlation-key ile yapilir (key userId'ye baglidir,
    // HMAC), yani donen odeme zaten cagirana aittir (F1). Tutar uyusmazsa VERIFY_FAILED -> red.
    public PaymentAttemptDecision OnChargeResult(
        PaymentOutcome outcome, string? providerPaymentId, decimal chargedPrice,
        DateTimeOffset now, CheckoutReconcile cfg)
    {
        if (Status == PaymentAttemptStatus.Succeeded) return new(PaymentAttemptAction.AlreadyCompleted);

        switch (outcome)
        {
            case PaymentOutcome.Success:
                ProviderPaymentId = providerPaymentId; // para cekildi -> her success'te izle (iz/refund)
                if (chargedPrice != Amount)
                {
                    // para alindi ama tutar uyusmaz: sessiz Failed DEGIL -> terminal ops gorunurluk
                    Status = PaymentAttemptStatus.NeedsReconciliation;
                    return new(PaymentAttemptAction.VerifyFailed); // tutar uyusmaz -> siparis yok
                }

                Status = PaymentAttemptStatus.Succeeded;
                return new(PaymentAttemptAction.CreateOrder);

            case PaymentOutcome.Failed:
                Status = PaymentAttemptStatus.Failed;
                return new(PaymentAttemptAction.NotifyFailed);

            default: // Ambiguous — cekim gitmis OLABILIR; asla kesin basarisiz deme
                Status = PaymentAttemptStatus.Unknown;
                AttemptCount = 0;
                var delay = NextBackoff(cfg);
                NextCheckAt = now + delay;
                return new(PaymentAttemptAction.ScheduleReconcile, delay);
        }
    }

    // US4: reconcile tick -> PG retrieve sonucu gecisi. Deadline dolduysa terminal (sonsuz degil).
    public PaymentAttemptDecision OnReconcileTick(
        PaymentOutcome outcome, string? providerPaymentId, decimal price,
        DateTimeOffset now, CheckoutReconcile cfg)
    {
        if (Status == PaymentAttemptStatus.Succeeded) return new(PaymentAttemptAction.AlreadyCompleted);
        if (IsTerminal) return new(PaymentAttemptAction.Terminal); // Failed/NeedsReconciliation

        AttemptCount++;

        switch (outcome)
        {
            case PaymentOutcome.Success:
                if (price != Amount)
                {
                    Status = PaymentAttemptStatus.Failed;
                    return new(PaymentAttemptAction.VerifyFailed);
                }

                Status = PaymentAttemptStatus.Succeeded;
                ProviderPaymentId = providerPaymentId;
                return new(PaymentAttemptAction.CreateOrder);

            case PaymentOutcome.Failed:
                Status = PaymentAttemptStatus.Failed;
                return new(PaymentAttemptAction.NotifyFailed);

            default: // pending / erisilemez
                if (now >= DeadlineAt)
                {
                    Status = PaymentAttemptStatus.NeedsReconciliation; // terminal, ops gorunurluk
                    return new(PaymentAttemptAction.Terminal);
                }

                var delay = NextBackoff(cfg);
                NextCheckAt = now + delay;
                return new(PaymentAttemptAction.ScheduleReconcile, delay);
        }
    }

    // Backoff: tick sirasina gore gecikme; liste tukenince son deger tekrar (5s,15s,60s,300s...).
    private TimeSpan NextBackoff(CheckoutReconcile cfg)
    {
        var steps = cfg.BackoffSeconds is { Length: > 0 } ? cfg.BackoffSeconds : [5, 15, 60, 300];
        var idx = Math.Min(AttemptCount, steps.Length - 1);
        return TimeSpan.FromSeconds(steps[idx]);
    }
}

public enum PaymentAttemptStatus
{
    Charging = 1,
    Unknown = 2,
    Succeeded = 3,
    Failed = 4,
    NeedsReconciliation = 5
}

// Saf karar cikti: handler yorumlar (order olustur / basarisiz bildir / reconcile zamanla / terminal).
public enum PaymentAttemptAction
{
    CreateOrder, // basari + tutar dogru -> siparis olustur
    NotifyFailed, // PG kesin basarisiz -> payment_failed
    VerifyFailed, // tutar/dogrulama uyusmaz -> red (siparis yok)
    ScheduleReconcile, // belirsiz -> ReconcileTick zamanla, kullaniciya pending
    Terminal, // deadline doldu / zaten terminal -> ops gorunurluk
    AlreadyCompleted // idempotent re-entry -> var olan siparis
}

public sealed record PaymentAttemptDecision(PaymentAttemptAction Action, TimeSpan? ReconcileDelay = null);