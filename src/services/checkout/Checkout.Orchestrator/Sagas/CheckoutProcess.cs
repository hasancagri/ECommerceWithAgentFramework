using static Shared.CheckoutMessages;

namespace Checkout.Orchestrator.Sagas;

// 049: checkout orchestration saga'sı (AYRI servis, broker-only — İlke I v1.11.0). State Marten
// belgesidir (Id = CheckoutId); adımlar RabbitMQ komut/yanıtıyla ilerler, her adım atomik persist
// (restart'a dayanır, FR-020). Her Handle NET ilerler: "yanıt geldi → sonraki komutu yayınla".
// Geçici hata SAGA'da değil — hedef BC fırlatır, Wolverine komutu yeniden dener (backoff, Program.cs).
// Saga yalnız kesin sonucu görür: başarı → ilerle; kalıcı hata → telafi. Sıra:
// CreateOrder→Authorize→Commit(×kalem)→Capture→Confirm→ClearBasket. Pivot = Capture; sonrası iptal YOK.
public class CheckoutProcess : Saga
{
    public Guid Id { get; set; }

    // Girişten taşınan (StartCheckout) — sonraki komutları kurmak için hangi servise gider:
    public Guid UserId { get; set; } // → Order, Payment, Stock, Basket (hepsi)
    public Guid OrderId { get; set; } // → Order (Confirm/Cancel) + Stock (Commit/Revert)
    public List<CheckoutItem> Items { get; set; } = []; // → Order (kalem oluştur) + Stock (kalem başına Commit)
    public decimal Amount { get; set; } // → Payment (Authorize)
    public OrderAddress? Address { get; set; } // → Order (CreateOrder)
    public string CardRef { get; set; } = ""; // → Payment (referans; mock yok sayar)
    public int Installments { get; set; } = 1; // → Payment (Authorize)

    // Yol boyu ÜRETİLEN (yanıttan yakalanır, sonraki adım kullanır):
    public List<CheckoutItem> CommittedItems { get; set; } = []; // → Stock (telafi LIFO Revert)
    public Guid? PaymentId { get; set; } // → Payment (Capture/Void)
    public string? AuthorizationRef { get; set; } // → Payment (kayıt/iz; komuta binmez)

    // Ödeme kaynağı (FR-030): TwoPhase=mock Authorize→Capture (web); AlreadyCaptured=dış PG çekti (chat)
    // → authorize/capture ATLANIR, sipariş zaten oluşturulmuştur, PaymentId null kalır.
    public PaymentMode PaymentMode { get; set; }

    // Orchestrator-içi kontrol:
    public int NextIndex { get; set; } // Stock commit döngü imleci
    public string Phase { get; set; } = CheckoutPhases.CreatingOrder; // watchdog + bayat-mesaj guard
    public bool CompensationFailed { get; set; } // kalıcı geri-alma alarmı
    public string CancelReason { get; set; } = ""; // → Order (CancelOrder sebep kodu) + loglar

    private string Key(string step) => $"{Id}:{step}";

    // Süreç başlar: sipariş oluşturma komutu + watchdog (dayanıklı zamanlayıcı; asılı kalma güvencesi).
    public static async Task<CheckoutProcess> Start(StartCheckout m, IMessageBus bus, CheckoutOptions opts)
    {
        var saga = new CheckoutProcess
        {
            Id = m.CheckoutId,
            UserId = m.UserId,
            Items = m.Items.ToList(),
            Amount = m.Amount,
            Address = m.Address,
            CardRef = m.CardRef,
            Installments = m.Installments,
            PaymentMode = m.PaymentMode
        };

        await bus.ScheduleAsync(new CheckoutTimedOut(m.CheckoutId), TimeSpan.FromSeconds(opts.WatchdogSeconds));

        if (m.PaymentMode == PaymentMode.AlreadyCaptured)
        {
            // Chat: ödeme dış PG ile çekildi + sipariş zaten oluşturuldu → doğrudan stok commit'e geç.
            saga.OrderId = m.OrderId;
            saga.Phase = CheckoutPhases.CommittingStock;
            var first = saga.Items[0];
            await bus.PublishAsync(new CommitStockCommand(m.CheckoutId, m.OrderId, first.ProductId, m.UserId, first.Quantity, $"{m.CheckoutId}:commit:0"));
            return saga;
        }

        // Web: siparişi orchestrator oluşturur (ilk adım).
        saga.Phase = CheckoutPhases.CreatingOrder;
        await bus.PublishAsync(new CreateOrderCommand(
            m.CheckoutId, m.UserId, m.Items, m.Amount, m.Address, m.CardRef, $"{m.CheckoutId}:create"));

        return saga;
    }

    // ---- MUTLU YOL: her Handle bir sonraki adımı yayınlar ----

    // 1. Sipariş oluştu → ödemeyi bloke et. Oluşmadıysa: telafi edilecek şey yok, bitir.
    public async Task Handle(OrderCreated r, IMessageBus bus)
    {
        if (!r.Success)
        {
            MarkCompleted();
            return;
        }

        OrderId = r.OrderId;
        Phase = CheckoutPhases.Authorizing;
        await bus.PublishAsync(new AuthorizePaymentCommand(Id, UserId, Amount, Installments, Key("auth")));
    }

    // 2. Ödeme bloke edildi → ilk kalemi commit et. Başarısız: stok/para yok → siparişi iptal et.
    public async Task Handle(PaymentAuthorized r, IMessageBus bus)
    {
        if (!r.Success)
        {
            Phase = CheckoutPhases.Compensating;
            CancelReason = CheckoutResourceConstants.CHECKOUT_PAYMENT_AUTHORIZE_FAILED;
            await bus.PublishAsync(new CancelOrderCommand(Id, OrderId, CancelReason, Key("cancel")));
            return;
        }

        PaymentId = r.PaymentId;
        AuthorizationRef = r.AuthorizationRef;
        Phase = CheckoutPhases.CommittingStock;
        NextIndex = 0;

        var first = Items[NextIndex];
        await bus.PublishAsync(new CommitStockCommand(Id, OrderId, first.ProductId, UserId, first.Quantity,
            Key($"commit:{NextIndex}")));
    }

    // 3. Kalem commit oldu → sonraki kalem, hepsi bittiyse ödemeyi tahsil et (Capture).
    //    Başarısız: telafi — committed varsa geri sar, yoksa (ilk kalem düştü) ödemeyi void et.
    public async Task Handle(StockCommitted r, IMessageBus bus)
    {
        if (Phase == CheckoutPhases.Compensating) return; // telafi başladı → bayat commit yanıtı

        if (!r.Success)
        {
            Phase = CheckoutPhases.Compensating;
            CancelReason = string.IsNullOrEmpty(r.MessageCode)
                ? CheckoutResourceConstants.CHECKOUT_STOCK_STEP_FAILED
                : r.MessageCode!;

            if (CommittedItems.Count > 0)
            {
                var last = CommittedItems[^1];
                await bus.PublishAsync(new RevertCommitStockCommand(Id, OrderId, last.ProductId, UserId, last.Quantity,
                    Key($"revert:{CommittedItems.Count}")));
            }
            else if (PaymentId is { } pid) // mock ödeme bloke edildi → void
                await bus.PublishAsync(new VoidPaymentCommand(Id, pid, Key("void")));
            else // AlreadyCaptured (dış PG): void yok, dış iade kapsam dışı → siparişi iptal et
                await bus.PublishAsync(new CancelOrderCommand(Id, OrderId, CancelReason, Key("cancel")));

            return;
        }

        CommittedItems.Add(Items[NextIndex]);
        NextIndex++;

        if (NextIndex < Items.Count)
        {
            var next = Items[NextIndex];
            await bus.PublishAsync(new CommitStockCommand(Id, OrderId, next.ProductId, UserId, next.Quantity,
                Key($"commit:{NextIndex}")));
            return;
        }

        // Tüm kalemler commit oldu. TwoPhase → ödemeyi tahsil et (Capture); AlreadyCaptured → doğrudan onay (pivot).
        if (PaymentMode == PaymentMode.AlreadyCaptured)
        {
            Phase = CheckoutPhases.Confirming;
            await bus.PublishAsync(new ConfirmOrderCommand(Id, OrderId, Key("confirm")));
        }
        else
        {
            Phase = CheckoutPhases.Capturing;
            await bus.PublishAsync(new CapturePaymentCommand(Id, PaymentId!.Value, Key("capture")));
        }
    }

    // 4. Ödeme tahsil edildi → siparişi onayla (pivot). Başarısız: telafi — tüm kalemler committed, geri sarmaya başla.
    public async Task Handle(PaymentCaptured r, IMessageBus bus)
    {
        if (!r.Success)
        {
            Phase = CheckoutPhases.Compensating;
            CancelReason = CheckoutResourceConstants.CHECKOUT_PAYMENT_CAPTURE_FAILED;
            var last = CommittedItems[^1];
            await bus.PublishAsync(new RevertCommitStockCommand(Id, OrderId, last.ProductId, UserId, last.Quantity,
                Key($"revert:{CommittedItems.Count}")));
            return;
        }

        Phase = CheckoutPhases.Confirming;
        await bus.PublishAsync(new ConfirmOrderCommand(Id, OrderId, Key("confirm")));
    }

    // 5. Sipariş onaylandı = PİVOT → sepeti temizle. Onay kalıcı başarısızsa: iptal ETME (para alındı), logla+bitir.
    public async Task Handle(OrderConfirmed r, IMessageBus bus, ILogger<CheckoutProcess> log)
    {
        if (!r.Success)
        {
            log.LogError("Checkout {Id}: onay kalıcı başarısız ama ödeme tahsil edildi — manuel müdahale.", Id);
            MarkCompleted();
            return;
        }

        Phase = CheckoutPhases.ClearingBasket;
        await bus.PublishAsync(new ClearBasketCommand(Id, UserId, Key("clear")));
    }

    // 6. Pivot sonrası sepet temizliği: başarı da hata da SÜRECİ BİTİRİR — sipariş Confirmed KALIR (FR-018).
    public Task Handle(BasketCleared r, ILogger<CheckoutProcess> log)
    {
        if (!r.Success)
            log.LogError("Checkout {Id}: sepet temizlenemedi; sipariş Confirmed KALIR (log-and-complete).", Id);

        MarkCompleted();
        return Task.CompletedTask;
    }

    // ---- TELAFİ (yalnız pivot öncesi): revert stok → void ödeme → iptal sipariş. Her aşama sonrakini yayınlar ----

    // Revert aşaması: kalan kalem varsa sonrakini geri sar; bittiyse ödemeyi void et (mock varsa),
    // yoksa (AlreadyCaptured — dış PG) doğrudan siparişi iptal et (dış iade kapsam dışı, loglanır).
    public async Task Handle(StockCommitReverted r, IMessageBus bus, ILogger<CheckoutProcess> log)
    {
        if (!r.Success)
        {
            CompensationFailed = true;
            log.LogCritical("Checkout {Id}: TELAFİ BAŞARISIZ — stok geri eklenemedi. Manuel müdahale.", Id);
        }

        if (CommittedItems.Count > 0) CommittedItems.RemoveAt(CommittedItems.Count - 1);

        if (CommittedItems.Count > 0)
        {
            var last = CommittedItems[^1];
            await bus.PublishAsync(new RevertCommitStockCommand(Id, OrderId, last.ProductId, UserId, last.Quantity,
                Key($"revert:{CommittedItems.Count}")));
            return;
        }

        if (PaymentId is { } pid)
            await bus.PublishAsync(new VoidPaymentCommand(Id, pid, Key("void")));
        else
            await bus.PublishAsync(new CancelOrderCommand(Id, OrderId, CancelReason, Key("cancel")));
    }

    // Void aşaması: ödeme serbest bırakıldı → sipariş iptali aşamasına geç.
    public async Task Handle(PaymentVoided r, IMessageBus bus)
    {
        if (!r.Success) CompensationFailed = true;
        await bus.PublishAsync(new CancelOrderCommand(Id, OrderId, CancelReason, Key("cancel")));
    }

    // İptal aşaması: sipariş iptal edildi → telafi tamam, süreç biter.
    public Task Handle(OrderCancelled r)
    {
        MarkCompleted();
        return Task.CompletedTask;
    }

    // ---- Watchdog: pivot öncesi → telafi (nereden başlarsa); Compensating → no-op; sonrası → iptal etme, bitir ----
    public async Task Handle(CheckoutTimedOut m, IMessageBus bus, ILogger<CheckoutProcess> log)
    {
        log.LogWarning("Checkout {Id}: watchdog doldu (faz {Phase}).", Id, Phase);

        if (Phase is CheckoutPhases.CreatingOrder or CheckoutPhases.Authorizing or CheckoutPhases.CommittingStock)
        {
            Phase = CheckoutPhases.Compensating;
            CancelReason = CheckoutResourceConstants.CHECKOUT_TIMEOUT;

            if (CommittedItems.Count > 0)
            {
                var last = CommittedItems[^1];
                await bus.PublishAsync(new RevertCommitStockCommand(Id, OrderId, last.ProductId, UserId, last.Quantity,
                    Key($"revert:{CommittedItems.Count}")));
            }
            else if (PaymentId is { } pid)
                await bus.PublishAsync(new VoidPaymentCommand(Id, pid, Key("void")));
            else if (OrderId != Guid.Empty)
                await bus.PublishAsync(new CancelOrderCommand(Id, OrderId, CancelReason, Key("cancel")));
            else
                MarkCompleted();

            return;
        }

        if (Phase == CheckoutPhases.Compensating) return;
        MarkCompleted();
    }

    // Tamamlanmış saga'ya geç gelen watchdog sessizce düşürülür (FR-026).
    public static void NotFound(CheckoutTimedOut m, ILogger<CheckoutProcess> log) =>
        log.LogDebug("Checkout {Id}: watchdog tamamlanmış saga'ya geldi, no-op.", m.CheckoutId);
}

// Süreç fazları — "sürecin neresindeyim" etiketi. Normal akışta yanıtlar sırayı sürükler; Phase asıl
// iki anormal durumda okunur: (a) watchdog dolunca pivot öncesi mi sonrası mı, (b) telafi sırasında
// bayat mesaj. Pivot çizgisi = Capturing (para tahsili) — öncesi geri sarılabilir, sonrası HAYIR.
public static class CheckoutPhases
{
    public const string CreatingOrder = "CreatingOrder"; // sipariş Pending oluşturuluyor — PİVOT ÖNCESİ
    public const string Authorizing = "Authorizing"; // ödeme bloke ediliyor (tutuldu, alınmadı) — PİVOT ÖNCESİ
    public const string CommittingStock = "CommittingStock"; // rezervasyonlar kalıcı düşüşe çevriliyor — PİVOT ÖNCESİ
    public const string Capturing = "Capturing"; // ödeme tahsil ediliyor = PİVOT (para alınıyor)
    public const string Confirming = "Confirming"; // sipariş onaylanıyor — PİVOT SONRASI
    public const string ClearingBasket = "ClearingBasket"; // sepet temizleniyor — PİVOT SONRASI, iptal YOK
    public const string Compensating = "Compensating"; // telafi: revert stok → void ödeme → iptal sipariş
}

// Watchdog mesajı (orchestrator-içi; broker'a çıkmaz).
public record CheckoutTimedOut([property: SagaIdentity] Guid CheckoutId);