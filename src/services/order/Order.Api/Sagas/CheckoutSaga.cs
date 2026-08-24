
namespace Order.Api.Sagas;

// --- 028: saga mesajlari (Order BC ici, durable local queue; RabbitMQ'ya cikmaz) ---

public record CheckoutItem(Guid ProductId, int Quantity);

public record StartCheckout([property: SagaIdentity] Guid OrderId, Guid UserId, List<CheckoutItem> Items);

public record CommitNextItem([property: SagaIdentity] Guid OrderId);

public record CompensateCheckout([property: SagaIdentity] Guid OrderId, string ReasonCode);

public record ClearBasketStep([property: SagaIdentity] Guid OrderId);

public record CheckoutTimedOut([property: SagaIdentity] Guid OrderId);

// Saga'nin surec muhasebesi (OrderStatus domain gercegidir; Phase gecici surec durumudur).
public static class CheckoutPhases
{
    public const string CommittingStock = "CommittingStock";
    public const string Compensating = "Compensating";
    public const string ClearingBasket = "ClearingBasket";
}

// Saf karar cikti tipi: handler bunu yorumlar (publish / schedule / cancel+complete / complete).
public sealed record NextStep(
    object? Message = null,
    TimeSpan? Delay = null,
    bool CompleteSaga = false,
    string? CancelWithReason = null);

// 028: checkout orchestration saga'si. State Marten belgesidir (Id = OrderId); adimlar mesajla
// ilerler, her adim atomik persist edilir (restart'a dayanir, FR-014). Karar mantigi saf
// On* metotlarindadir (birim testlenir); handler'lar yalnizca gRPC cagirir ve karari uygular.
public class CheckoutSaga : Saga
{
    public const int MaxAttempts = 3;
    public static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public List<CheckoutItem> Items { get; set; } = [];
    public int NextIndex { get; set; }
    public List<CheckoutItem> CommittedItems { get; set; } = [];
    public int Attempt { get; set; }
    public string Phase { get; set; } = CheckoutPhases.CommittingStock;
    public bool CompensationFailed { get; set; }

    [JsonIgnore] public bool AllItemsCommitted => NextIndex >= Items.Count;
    [JsonIgnore] public CheckoutItem CurrentItem => Items[NextIndex];

    // --- saf karar cekirdegi (birim testlerin hedefi) ---

    // Kalem commit sonucu: basari -> sonraki kalem; teknik hata -> sinirli retry; is hatasi -> telafi.
    public NextStep OnCommitResult(CommitResult result)
    {
        if (result.Success)
        {
            CommittedItems.Add(CurrentItem);
            NextIndex++;
            Attempt = 0;

            if (AllItemsCommitted)
            {
                Phase = CheckoutPhases.ClearingBasket;
                return new NextStep(Message: new ClearBasketStep(Id));
            }

            return new NextStep(Message: new CommitNextItem(Id));
        }

        if (result.Code == StockCommitClientProxy.CommitUnavailable && Attempt < MaxAttempts)
        {
            Attempt++;
            return new NextStep(Message: new CommitNextItem(Id), Delay: RetryDelay);
        }

        Phase = CheckoutPhases.Compensating;
        Attempt = 0;
        var reason = string.IsNullOrEmpty(result.Code) ? OrderResourceConstants.ORDER_STOCK_STEP_FAILED : result.Code;
        return new NextStep(Message: new CompensateCheckout(Id, reason));
    }

    // Telafi adimi: kalem basina TEK revert. Basari -> sonraki kalem icin ayni mesaj; teknik hata ->
    // sinirli retry; kalici hata -> CompensationFailed (FR-013). Kalem kalmayinca iptal + bitis.
    public NextStep OnRevertResult(CommitResult? result, string reasonCode)
    {
        if (result is not null)
        {
            if (result.Success)
            {
                CommittedItems.RemoveAt(CommittedItems.Count - 1);
                Attempt = 0;
            }
            else if (result.Code == StockCommitClientProxy.CommitUnavailable && Attempt < MaxAttempts)
            {
                Attempt++;
                return new NextStep(Message: new CompensateCheckout(Id, reasonCode), Delay: RetryDelay);
            }
            else
            {
                CompensationFailed = true; // alarm logu handler'da
                return new NextStep(CompleteSaga: true, CancelWithReason: reasonCode);
            }
        }

        if (CommittedItems.Count > 0)
            return new NextStep(Message: new CompensateCheckout(Id, reasonCode));

        return new NextStep(CompleteSaga: true, CancelWithReason: reasonCode);
    }

    // Pivot-sonrasi adim: basarisizlik siparisi ASLA iptal etmez (FR-009); sinirli retry + bitis.
    public NextStep OnClearBasketResult(bool success)
    {
        if (success) return new NextStep(CompleteSaga: true);

        if (Attempt < MaxAttempts)
        {
            Attempt++;
            return new NextStep(Message: new ClearBasketStep(Id), Delay: RetryDelay);
        }

        return new NextStep(CompleteSaga: true); // log-and-complete; siparis Confirmed KALIR
    }

    // Watchdog karari (FR-011/012): pivot sonrasi iptal YASAK; telafi surerken no-op.
    public NextStep OnTimeout()
    {
        if (Phase == CheckoutPhases.ClearingBasket)
            return new NextStep(CompleteSaga: true);

        if (Phase == CheckoutPhases.Compensating)
            return new NextStep();

        Phase = CheckoutPhases.Compensating;
        Attempt = 0;
        return new NextStep(Message: new CompensateCheckout(Id, OrderResourceConstants.ORDER_TIMEOUT));
    }

    // --- Wolverine handler'lari (ince: gRPC cagir + karari uygula) ---

    // Saga dogumu: CreateOrder StartCheckout yayinlar. Watchdog ayni anda kurulur — Postgres'te
    // kalici zarf; uygulama kapansa da vadesinde (ya da restart sonrasi) teslim edilir.
    public static async Task<CheckoutSaga> Start(
        StartCheckout message,
        IMessageBus bus,
        Checkout checkout)
    {
        var saga = new CheckoutSaga
        {
            Id = message.OrderId,
            UserId = message.UserId,
            Items = message.Items
        };

        await bus.PublishAsync(new CommitNextItem(message.OrderId));

        await bus.ScheduleAsync(new CheckoutTimedOut(message.OrderId), TimeSpan.FromSeconds(checkout.WatchdogSeconds));

        return saga;
    }

    public async Task Handle(
        CommitNextItem message,
        StockCommitClientProxy stockClient,
        IMessageBus bus,
        ILogger<CheckoutSaga> logger,
        CancellationToken ct)
    {
        if (Phase != CheckoutPhases.CommittingStock) return; // bayat mesaj (telafi basladi)

        if (AllItemsCommitted)
        {
            Phase = CheckoutPhases.ClearingBasket;
            Attempt = 0;
            await bus.PublishAsync(new ClearBasketStep(Id));
            return;
        }

        var item = CurrentItem;
        var result = await stockClient.CommitAsync(item.ProductId, UserId, item.Quantity, Id, ct);
        var step = OnCommitResult(result);

        if (!result.Success)
            logger.LogWarning("Checkout {OrderId}: kalem {ProductId} commit sonucu {Code}; karar: {Step}.",
                Id, item.ProductId, result.Code, step.Message?.GetType().Name);

        await DispatchAsync(step, bus, session: null, logger, ct);
    }

    public async Task Handle(
        CompensateCheckout message,
        StockCommitClientProxy stockClient,
        IDocumentSession session,
        IMessageBus bus,
        ILogger<CheckoutSaga> logger,
        CancellationToken ct)
    {
        Phase = CheckoutPhases.Compensating;

        CommitResult? result = null;
        if (CommittedItems.Count > 0)
        {
            var item = CommittedItems[^1];
            result = await stockClient.RevertCommitAsync(item.ProductId, UserId, item.Quantity, Id, ct);
        }

        var step = OnRevertResult(result, message.ReasonCode);

        if (CompensationFailed)
            logger.LogCritical(
                "Checkout {OrderId}: TELAFI BASARISIZ — stok geri eklenemedi ({Code}). Manuel mudahale gerekir.",
                Id, result?.Code);

        await DispatchAsync(step, bus, session, logger, ct);
    }

    public async Task Handle(
        ClearBasketStep message,
        BasketClearClientProxy basketClient,
        IDocumentSession session,
        IMessageBus bus,
        ILogger<CheckoutSaga> logger,
        CancellationToken ct)
    {
        // Pivot: Confirm sepet temizliginden ONCE yazilir (idempotent — zaten Confirmed ise gecilir).
        var order = await session.LoadAsync<Domains.Orders.Order>(Id, ct);
        if (order is not null && order.Status == OrderStatus.Pending)
        {
            order.Confirm();
            session.Store(order);

            // 048: siparis odeme onayli olarak tamamlandi (pivot) — Personalization satin-alma sinyali.
            // Tam bir kez (Pending→Confirmed bloku) yayilir; transactional outbox ile guvenli.
            // Category/Brand Order'da yok → null (D3). OrderedAt = siparis tarihi (CreatedTime).
            var items = order.OrderItems
                .Select(oi => new IntegrationEvents.OrderCompletedItem(
                    oi.ProductId, oi.Quantity, oi.UnitPrice))
                .ToList();
            await bus.PublishAsync(new IntegrationEvents.OrderCompleted(
                order.Id, UserId, new DateTimeOffset(order.CreatedTime, TimeSpan.Zero), items));
        }

        var result = await basketClient.ClearAsync(UserId, Id, ct);
        var step = OnClearBasketResult(result.Success);

        if (!result.Success)
            logger.LogWarning("Checkout {OrderId}: sepet temizligi basarisiz ({Code}), deneme {Attempt}/{Max}.",
                Id, result.Code, Attempt, MaxAttempts);
        if (!result.Success && step.CompleteSaga)
            logger.LogError("Checkout {OrderId}: sepet temizligi retry tukendi; siparis Confirmed KALIR.", Id);

        await DispatchAsync(step, bus, session, logger, ct);
    }

    public async Task Handle(
        CheckoutTimedOut message,
        IDocumentSession session,
        IMessageBus bus,
        ILogger<CheckoutSaga> logger,
        CancellationToken ct)
    {
        var step = OnTimeout();

        if (Phase == CheckoutPhases.ClearingBasket && step.CompleteSaga)
            logger.LogError("Checkout {OrderId}: watchdog pivot sonrasi doldu; sepet temizligi eksik kalabilir.", Id);
        else if (step.Message is not null)
            logger.LogWarning("Checkout {OrderId}: watchdog doldu; telafi + iptal basliyor.", Id);

        await DispatchAsync(step, bus, session, logger, ct);
    }

    // Tamamlanmis saga'ya gec gelen mesajlar sessizce dusurulur (FR-012).
    public static void NotFound(CheckoutTimedOut message, ILogger<CheckoutSaga> logger) =>
        logger.LogDebug("Checkout {OrderId}: watchdog tamamlanmis saga'ya geldi, no-op.", message.OrderId);

    public static void NotFound(CommitNextItem message, ILogger<CheckoutSaga> logger) =>
        logger.LogDebug("Checkout {OrderId}: bayat CommitNextItem, no-op.", message.OrderId);

    public static void NotFound(CompensateCheckout message, ILogger<CheckoutSaga> logger) =>
        logger.LogDebug("Checkout {OrderId}: bayat CompensateCheckout, no-op.", message.OrderId);

    public static void NotFound(ClearBasketStep message, ILogger<CheckoutSaga> logger) =>
        logger.LogDebug("Checkout {OrderId}: bayat ClearBasketStep, no-op.", message.OrderId);

    // Karari uygular: mesaj yayinla/planla, gerekiyorsa siparisi iptal et, saga'yi bitir.
    private async Task DispatchAsync(
        NextStep step, IMessageBus bus, IDocumentSession? session,
        ILogger<CheckoutSaga> logger, CancellationToken ct)
    {
        if (step.Message is not null)
        {
            if (step.Delay is { } delay) await bus.ScheduleAsync(step.Message, delay);
            else await bus.PublishAsync(step.Message);
        }

        if (step.CancelWithReason is { } reason && session is not null)
        {
            var order = await session.LoadAsync<Domains.Orders.Order>(Id, ct);
            if (order is null)
                logger.LogError("Checkout {OrderId}: iptal edilecek siparis bulunamadi.", Id);
            else if (order.Cancel(reason).IsSuccess)
                session.Store(order);
        }

        if (step.CompleteSaga) MarkCompleted();
    }
}