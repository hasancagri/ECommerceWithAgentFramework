namespace Personalization.Api.Domains.PurchaseSignals;

// 048: tamamlanmis (odeme onayli) siparisin kisisellestirme gorunumu. Kayipsiz, nadir.
// Id = OrderId (idempotent dogal anahtar): ayni OrderCompleted event'i yeniden teslim
// edilse handler once LoadAsync ile no-op yapar; ikinci Store da upsert (cift savunma).
public class PurchaseSignal : AggregateRoot
{
    public Guid UserId { get; private set; }
    public DateTimeOffset OrderedAt { get; private set; }
    public IReadOnlyList<PurchaseSignalItem> Items { get; private set; } = [];

    private PurchaseSignal()
    {
    }

    /// <summary>Odeme onayli tamamlanan siparisten satin-alma sinyali olusturur; en az 1 kalem, gecerli referanslar.</summary>
    public static ResultDomain<PurchaseSignal> Create(
        Guid orderId, Guid userId, DateTimeOffset orderedAt, IReadOnlyList<PurchaseSignalItem> items)
    {
        var messages = new List<MessageItem>();

        if (orderId == Guid.Empty)
            messages.Add(new MessageItem
                { Property = nameof(orderId), Code = PersonalizationResourceConstants.PURCHASE_SIGNAL_REFERENCE_INVALID });

        if (userId == Guid.Empty)
            messages.Add(new MessageItem
                { Property = nameof(UserId), Code = PersonalizationResourceConstants.PURCHASE_SIGNAL_REFERENCE_INVALID });

        if (items is null || items.Count == 0)
            messages.Add(new MessageItem
                { Property = nameof(Items), Code = PersonalizationResourceConstants.PURCHASE_SIGNAL_ITEMS_REQUIRED });

        if (messages.Count > 0)
            return ResultDomain<PurchaseSignal>.Error(messages);

        return ResultDomain<PurchaseSignal>.Ok(new PurchaseSignal
        {
            Id = orderId,
            UserId = userId,
            OrderedAt = orderedAt,
            Items = items!.ToList(),
        });
    }
}