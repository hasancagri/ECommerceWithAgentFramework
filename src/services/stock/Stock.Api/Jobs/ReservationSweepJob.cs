namespace Stock.Api.Jobs;

// 012 (US4): TTL suresi gecmis rezervasyonlari fiziksel siler ve her biri icin ReservationExpired
// yayinlar (Basket sepet satirini siler). Lazy filtre zaten Available'i dogru tutuyor; bu is
// kaydi temizler + event'i uretir. Her stok kendi session'inda islenir (optimistic concurrency izolasyonu).
public sealed class ReservationSweepJob(IDocumentStore store, IMessageBus bus, ILogger<ReservationSweepJob> logger)
{
    [AutomaticRetry(Attempts = 1)]
    public async Task RunAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        await using var readSession = store.QuerySession();
        var ids = await readSession.Query<ProductStock>().Select(s => s.Id).ToListAsync(ct);

        var expiredEvents = new List<IntegrationEvents.ReservationExpired>();

        foreach (var id in ids)
        {
            try
            {
                await using var session = store.LightweightSession();
                var stock = await session.LoadAsync<ProductStock>(id, ct);
                if (stock is null) continue;

                var purge = stock.PurgeExpired(now);
                if (!purge.IsSuccess) continue;
                var expired = purge.Data!;
                if (expired.Count == 0) continue;

                session.Store(stock);
                await session.SaveChangesAsync(ct);

                foreach (var reservation in expired)
                    expiredEvents.Add(new IntegrationEvents.ReservationExpired(stock.ProductId, reservation.UserId));
            }
            catch (Marten.Exceptions.ConcurrentUpdateException)
            {
                // Eszamanli degisim: bir sonraki sweep yakalar.
            }
        }

        foreach (var evt in expiredEvents)
            await bus.PublishAsync(evt);

        if (expiredEvents.Count > 0)
            logger.LogInformation("Reservation sweep: {Count} suresi gecmis rezervasyon serbest birakildi.",
                expiredEvents.Count);
    }
}