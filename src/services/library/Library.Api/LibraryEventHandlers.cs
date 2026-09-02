namespace Library.Api;

// 060: Library'nin event yüzü — ProductChangedEvent (fiyat tetiği) + NotificationSent (iz).
// Wolverine *EventHandlers (çoğul) adını keşfetMEZ — Program.cs IncludeType ile dahil eder.
[Transactional]
public class LibraryEventHandlers
{
    /// <summary>
    /// Fiyat değişiminden HEMEN SONRA koşar (Catalog admin kaydeder kaydetmez, event'le).
    /// Alarm kurma burada DEĞİL (o CreatePriceAlarm komutu); burada mevcut alarm kayıtları bulunur
    /// ve alarm başına PriceAlarmTriggered yayınlanır — mail zincirinin başlangıcı.
    /// Tetik = OldPrice dolu VE yeni fiyat farklı (yön bakılmaz — R2). Alarm mutasyonu YOK:
    /// yaşayan abonelik (FR-004), kullanıcı kaldırana dek her değişimde tetiklenir. Alarm yoksa sessiz.
    /// </summary>
    public async Task Handle(
        IntegrationEvents.ProductChangedEvent evt,
        IDocumentSession session,
        IMessageBus bus,
        CancellationToken ct)
    {
        if (evt.OldPrice is null || evt.OldPrice == evt.Price)
            return;

        var alarms = await session.Query<PriceAlarm>()
            .Where(x => x.ProductId == evt.ProductId)
            .ToListAsync(ct);

        foreach (var alarm in alarms)
            await bus.PublishAsync(new IntegrationEvents.PriceAlarmTriggered(
                alarm.Id, alarm.UserId, alarm.Email,
                evt.ProductId, evt.Name,
                evt.OldPrice.Value, evt.Price));
    }

    /// <summary>
    /// Mail gönderiminden HEMEN SONRA koşar (NotificationAgent sonucu NotificationSent ile geri yayınlar).
    /// Her gönderim denemesi kalıcı iz olur — append-only NotificationRecord (FR-007);
    /// "sent" / "no-email" / hata özeti. Alarm silinse de iz kalır.
    /// </summary>
    public void Handle(
        IntegrationEvents.NotificationSent evt,
        IDocumentSession session)
    {
        session.Store(new Domains.PriceAlarms.Entities.NotificationRecord
        {
            UserId = evt.UserId,
            ProductId = evt.ProductId,
            Email = evt.Email,
            Success = evt.Success,
            Detail = evt.Detail,
            CreatedAtUtc = DateTime.UtcNow,
        });
    }
}