namespace Checkout.Orchestrator.Sagas;

// 049: checkout orchestration saga'sı (AYRI servis, broker-only — İlke I v1.11.0). State Marten
// belgesidir (Id = CheckoutId); adımlar RabbitMQ komut/yanıtıyla ilerler, her adım atomik persist
// (restart'a dayanır, FR-020). Her Handle NET ilerler: "yanıt geldi → sonraki komutu yayınla".
// Geçici hata SAGA'da değil — hedef BC fırlatır, Wolverine komutu yeniden dener (backoff, Program.cs).
// Saga yalnız kesin sonucu görür: başarı → ilerle; kalıcı hata → telafi. Sıra:
// CreateOrder→Commit(×kalem)→Charge→Confirm→ClearBasket. Pivot = Charge (tek-faz tahsilat, SON adım);
// öncesi geri-alınabilir (stok revert + sipariş cancel, para hareket etmez), sonrası geri-alma YOK
// (void/refund söküldü — kullanıcı kararı 2026-08-26).
public class CheckoutProcess : Saga
{
    public Guid Id { get; set; }

    // Girişten taşınan (StartCheckout) — sonraki komutları kurmak için hangi servise gider:
    public Guid UserId { get; set; } // → Order, Payment, Stock, Basket (hepsi)
    public Guid OrderId { get; set; } // → Order (Confirm/Cancel) + Stock (Commit/Revert)
    public List<CheckoutItem> Items { get; set; } = []; // → Order (kalem oluştur) + Stock (kalem başına Commit)
    public decimal Amount { get; set; } // → Payment (Charge)
    public OrderAddress? Address { get; set; } // → Order (CreateOrder)
    public string CardRef { get; set; } = ""; // → Payment (referans; mock yok sayar)
    public int Installments { get; set; } = 1; // → Payment (Charge)

    // Yol boyu ÜRETİLEN (yanıttan yakalanır, sonraki adım kullanır):
    public List<CheckoutItem> CommittedItems { get; set; } = []; // → Stock (telafi LIFO Revert)
    public Guid? PaymentId { get; set; } // Charge yanıtından (iz/kayıt; komuta binmez — void/capture yok)

    // Ödeme kaynağı (FR-030): Charge=mock tek-faz tahsilat (web); AlreadyCaptured=dış PG çekti (chat)
    // → charge ATLANIR, sipariş zaten oluşturulmuştur, PaymentId null kalır.
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

    // 1. Sipariş oluştu → ilk kalemi commit et (ödeme EN SONA taşındı). Oluşmadıysa: telafi yok, bitir.
    public async Task Handle(OrderCreated r, IMessageBus bus)
    {
        if (!r.Success)
        {
            MarkCompleted();
            return;
        }

        OrderId = r.OrderId;
        Phase = CheckoutPhases.CommittingStock;
        NextIndex = 0;

        var first = Items[NextIndex];
        await bus.PublishAsync(new CommitStockCommand(Id, OrderId, first.ProductId, UserId, first.Quantity,
            Key($"commit:{NextIndex}")));
    }

    // 2. Kalem commit oldu → sonraki kalem; hepsi bittiyse ödemeyi çek (Charge=pivot) / doğrudan onay
    //    (AlreadyCaptured). Başarısız: telafi — committed varsa geri sar (LIFO), yoksa siparişi iptal et.
    //    Ödeme henüz çekilmedi (charge en sonda) → void yok.
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
            else // ilk kalem düştü — commit edilmiş stok yok, ödeme de çekilmedi → siparişi iptal et
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

        // Tüm kalemler commit oldu. Charge → ödemeyi çek (pivot); AlreadyCaptured → doğrudan onay (pivot geçildi).
        if (PaymentMode == PaymentMode.AlreadyCaptured)
        {
            Phase = CheckoutPhases.Confirming;
            await bus.PublishAsync(new ConfirmOrderCommand(Id, OrderId, Key("confirm")));
        }
        else
        {
            Phase = CheckoutPhases.Charging;
            await bus.PublishAsync(new ChargePaymentCommand(Id, UserId, Amount, Installments, Key("charge")));
        }
    }

    // 3. Ödeme çekildi = PİVOT → siparişi onayla. Başarısız: para hareket ETMEDİ → telafi (tüm kalemler
    //    committed, geri sarmaya başla); void/refund yok.
    public async Task Handle(PaymentCharged r, IMessageBus bus)
    {
        if (!r.Success)
        {
            Phase = CheckoutPhases.Compensating;
            CancelReason = CheckoutResourceConstants.CHECKOUT_PAYMENT_CHARGE_FAILED;

            if (CommittedItems.Count > 0)
            {
                var last = CommittedItems[^1];
                await bus.PublishAsync(new RevertCommitStockCommand(Id, OrderId, last.ProductId, UserId, last.Quantity,
                    Key($"revert:{CommittedItems.Count}")));
            }
            else
                await bus.PublishAsync(new CancelOrderCommand(Id, OrderId, CancelReason, Key("cancel")));

            return;
        }

        PaymentId = r.PaymentId;
        Phase = CheckoutPhases.Confirming;
        await bus.PublishAsync(new ConfirmOrderCommand(Id, OrderId, Key("confirm")));
    }

    // 4. Sipariş onaylandı (pivot geçildi) → sepeti temizle. Onay kalıcı başarısızsa: iptal ETME (para alındı), logla+bitir.
    public async Task Handle(OrderConfirmed r, IMessageBus bus, ILogger<CheckoutProcess> log)
    {
        if (!r.Success)
        {
            log.LogError("Checkout {Id}: onay kalıcı başarısız ama ödeme çekildi — manuel müdahale.", Id);
            MarkCompleted();
            return;
        }

        Phase = CheckoutPhases.ClearingBasket;
        await bus.PublishAsync(new ClearBasketCommand(Id, UserId, Key("clear")));
    }

    // 5. Pivot sonrası sepet temizliği: başarı da hata da SÜRECİ BİTİRİR — sipariş Confirmed KALIR (FR-018).
    public Task Handle(BasketCleared r, ILogger<CheckoutProcess> log)
    {
        if (!r.Success)
            log.LogError("Checkout {Id}: sepet temizlenemedi; sipariş Confirmed KALIR (log-and-complete).", Id);

        MarkCompleted();
        return Task.CompletedTask;
    }

    // ---- TELAFİ (yalnız pivot öncesi): revert stok → iptal sipariş. Her aşama sonrakini yayınlar.
    //      Ödeme void/refund YOK — charge pivottur, öncesi para hareket etmez. ----

    // Revert aşaması: kalan kalem varsa sonrakini geri sar; bittiyse siparişi iptal et (dış PG iadesi —
    // AlreadyCaptured — kapsam dışı, iptal yalnız sipariş durumunu düzeltir, loglanır).
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

        await bus.PublishAsync(new CancelOrderCommand(Id, OrderId, CancelReason, Key("cancel")));
    }

    // İptal aşaması: sipariş iptal edildi → telafi tamam, süreç biter.
    public Task Handle(OrderCancelled r)
    {
        MarkCompleted();
        return Task.CompletedTask;
    }

    // ---- Watchdog: pivot öncesi (CreatingOrder/CommittingStock) → telafi; Charging (pivot, çekim uçuşta) +
    //      sonrası → iptal etme, bitir (ambiguity: para çekilmiş olabilir); Compensating → no-op ----
    public async Task Handle(CheckoutTimedOut m, IMessageBus bus, ILogger<CheckoutProcess> log)
    {
        log.LogWarning("Checkout {Id}: watchdog doldu (faz {Phase}).", Id, Phase);

        if (Phase is CheckoutPhases.CreatingOrder or CheckoutPhases.CommittingStock)
        {
            Phase = CheckoutPhases.Compensating;
            CancelReason = CheckoutResourceConstants.CHECKOUT_TIMEOUT;

            if (CommittedItems.Count > 0)
            {
                var last = CommittedItems[^1];
                await bus.PublishAsync(new RevertCommitStockCommand(Id, OrderId, last.ProductId, UserId, last.Quantity,
                    Key($"revert:{CommittedItems.Count}")));
            }
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
// bayat mesaj. Pivot çizgisi = Charging (para tahsili, SON adım) — öncesi geri sarılabilir, sonrası HAYIR.
public static class CheckoutPhases
{
    public const string CreatingOrder = "CreatingOrder"; // sipariş Pending oluşturuluyor — PİVOT ÖNCESİ
    public const string CommittingStock = "CommittingStock"; // rezervasyonlar kalıcı düşüşe çevriliyor — PİVOT ÖNCESİ
    public const string Charging = "Charging"; // ödeme tek-faz tahsil ediliyor = PİVOT (para alınıyor)
    public const string Confirming = "Confirming"; // sipariş onaylanıyor — PİVOT SONRASI
    public const string ClearingBasket = "ClearingBasket"; // sepet temizleniyor — PİVOT SONRASI, iptal YOK
    public const string Compensating = "Compensating"; // telafi: revert stok → iptal sipariş (void/refund yok)
}

// Watchdog mesajı (orchestrator-içi; broker'a çıkmaz).
public record CheckoutTimedOut([property: SagaIdentity] Guid CheckoutId);
