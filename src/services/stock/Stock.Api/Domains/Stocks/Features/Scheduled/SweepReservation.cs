namespace Stock.Api.Domains.Stocks.Features.Scheduled;

// 026: durable süre-sonu tetiği. ReserveStock, rezervasyon TTL bitiş anına bunu ScheduleAsync eder;
// tam o an düşer (polling penceresi yok, restart'a dayanır — UseDurableLocalQueues).
public record SweepReservation(Guid ProductId, Guid UserId);

// Fire'da tek stoğun süresi geçmiş rezervasyonlarını idempotent purge eder + ReservationExpired
// yayınlar (Basket sepet satırını siler). Bayat tetik (yenilenmiş rezervasyon) fire olunca
// PurgeExpired yalnız !IsActiveAt siler => aktif rezervasyon korunur; nesil-belirteci gerekmez.
// [Transactional]: save + ReservationExpired outbox'a Marten commit'iyle atomik yazılır.
[Transactional]
public class SweepReservationHandler
{
    public async Task Handle(
        SweepReservation message,
        IDocumentSession session,
        IMessageBus bus,
        ILogger<SweepReservationHandler> logger,
        CancellationToken ct)
    {
        var stock = await session.Query<ProductStock>()
            .FirstOrDefaultAsync(x => x.ProductId == message.ProductId, ct);
        if (stock is null) return;

        var expired = stock.PurgeExpired(DateTimeOffset.UtcNow);
        if (expired.Count == 0) return; // aktif/yenilenmiş => no-op

        session.Store(stock);

        foreach (var reservation in expired)
            await bus.PublishAsync(new IntegrationEvents.ReservationExpired(stock.ProductId, reservation.UserId));

        logger.LogInformation(
            "Durable sweep: {ProductId} icin {Count} suresi gecmis rezervasyon serbest birakildi.",
            stock.ProductId, expired.Count);
    }
}