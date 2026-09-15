namespace Checkout.Orchestrator.Sagas;

// 049/077: checkout orchestration saga'sı (AYRI servis, broker-only — İlke I v1.11.0). State Marten
// belgesidir (Id = CheckoutId); adımlar RabbitMQ komut/yanıtıyla ilerler, her adım atomik persist
// (restart'a dayanır, FR-020). 077: ödeme hosted-CF ile ÖNCEDEN çekildi (Order.Api PaymentSucceeded'i
// tüketip StartCheckout yayınlar) → sipariş ZATEN oluşturulmuştur (OrderId dolu). Saga charge ÇEKMEZ.
// Sıra: Commit(×kalem)→Confirm→ClearBasket. Pivot = ödeme (saga DIŞINDA, callback anında geçildi) →
// Commit/Confirm/ClearBasket pivot-sonrası sayılır; stok commit başarısızsa telafi (LIFO revert + cancel;
// para dış PG'de, iade kapsam dışı, iptal yalnız sipariş durumunu düzeltir).
public class CheckoutProcess : Saga
{
    public Guid Id { get; set; }

    // Girişten taşınan (StartCheckout):
    public Guid UserId { get; set; } // → Stock (Commit/Revert) + Basket (Clear)
    public Guid OrderId { get; set; } // → Order (Confirm/Cancel) + Stock (Commit/Revert)
    public List<CheckoutItem> Items { get; set; } = []; // → Stock (kalem başına Commit)

    // Yol boyu ÜRETİLEN:
    public List<CheckoutItem> CommittedItems { get; set; } = []; // → Stock (telafi LIFO Revert)

    // Orchestrator-içi kontrol:
    public int NextIndex { get; set; } // Stock commit döngü imleci
    public string Phase { get; set; } = CheckoutPhases.CommittingStock; // watchdog + bayat-mesaj guard
    public bool CompensationFailed { get; set; } // kalıcı geri-alma alarmı
    public string CancelReason { get; set; } = ""; // → Order (CancelOrder sebep kodu) + loglar

    private string Key(string step) => $"{Id}:{step}";

    // Süreç başlar: ödeme zaten çekildi + sipariş oluşturuldu → doğrudan stok commit'e geç + watchdog.
    public static async Task<CheckoutProcess> Start(StartCheckout m, IMessageBus bus, CheckoutOptions opts)
    {
        var saga = new CheckoutProcess
        {
            Id = m.CheckoutId,
            UserId = m.UserId,
            Items = m.Items.ToList(),
            OrderId = m.OrderId,
            Phase = CheckoutPhases.CommittingStock
        };

        await bus.ScheduleAsync(new CheckoutTimedOut(m.CheckoutId), TimeSpan.FromSeconds(opts.WatchdogSeconds));

        var first = saga.Items[0];
        await bus.PublishAsync(new CommitStockCommand(m.CheckoutId, m.OrderId, first.ProductId, m.UserId, first.Quantity, $"{m.CheckoutId}:commit:0"));
        return saga;
    }

    // ---- MUTLU YOL: her Handle bir sonraki adımı yayınlar ----

    // Kalem commit oldu → sonraki kalem; hepsi bittiyse siparişi onayla (ödeme öncedendir → charge yok).
    // Başarısız: telafi — committed varsa LIFO geri sar, yoksa siparişi iptal et.
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
            else // ilk kalem düştü — commit edilmiş stok yok → siparişi iptal et
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

        // Tüm kalemler commit oldu → siparişi onayla (ödeme öncedendir; charge adımı yok).
        Phase = CheckoutPhases.Confirming;
        await bus.PublishAsync(new ConfirmOrderCommand(Id, OrderId, Key("confirm")));
    }

    // Sipariş onaylandı → sepeti temizle. Onay kalıcı başarısızsa: iptal ETME (ödeme alındı), logla+bitir.
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

    // Sepet temizliği: başarı da hata da SÜRECİ BİTİRİR — sipariş Confirmed KALIR (FR-018).
    public Task Handle(BasketCleared r, ILogger<CheckoutProcess> log)
    {
        if (!r.Success)
            log.LogError("Checkout {Id}: sepet temizlenemedi; sipariş Confirmed KALIR (log-and-complete).", Id);

        MarkCompleted();
        return Task.CompletedTask;
    }

    // ---- TELAFİ: revert stok → iptal sipariş. Her aşama sonrakini yayınlar. Ödeme dış PG'de (iade kapsam dışı). ----

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

    // ---- Watchdog: CommittingStock (pivot-öncesi telafi mümkün) → telafi; Confirming/ClearingBasket
    //      (ödeme alındı) → iptal etme, bitir; Compensating → no-op ----
    public async Task Handle(CheckoutTimedOut m, IMessageBus bus, ILogger<CheckoutProcess> log)
    {
        log.LogWarning("Checkout {Id}: watchdog doldu (faz {Phase}).", Id, Phase);

        if (Phase == CheckoutPhases.CommittingStock)
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

// Süreç fazları — "sürecin neresindeyim" etiketi. 077: ödeme saga dışında (hosted-CF callback) çekildi →
// CommittingStock = telafi mümkün son faz; Confirming/ClearingBasket = ödeme alındı, iptal YOK.
public static class CheckoutPhases
{
    public const string CommittingStock = "CommittingStock"; // rezervasyonlar kalıcı düşüşe çevriliyor — telafi mümkün
    public const string Confirming = "Confirming"; // sipariş onaylanıyor — ödeme alındı, iptal YOK
    public const string ClearingBasket = "ClearingBasket"; // sepet temizleniyor — iptal YOK
    public const string Compensating = "Compensating"; // telafi: revert stok → iptal sipariş (dış iade yok)
}

// Watchdog mesajı (orchestrator-içi; broker'a çıkmaz).
public record CheckoutTimedOut([property: SagaIdentity] Guid CheckoutId);
