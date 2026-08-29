namespace Personalization.Api.Domains.PurchaseSignals.ValueObjects;

// 048: satin-alma sinyali kalemi. VO — kendi invariant'ini korur (adet>0, tutar>=0).
// Category/Brand nullable: Order bunlari tutmuyorsa null gelir (BC izolasyonu, D3).
public record PurchaseSignalItem
{
    public Guid ProductId { get; private set; }
    public string? Category { get; private set; }
    public string? Brand { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }

    private PurchaseSignalItem()
    {
    }

    public static ResultDomain<PurchaseSignalItem> Create(
        Guid productId, string? category, string? brand, int quantity, decimal unitPrice)
    {
        var messages = new List<MessageItem>();

        if (productId == Guid.Empty)
            messages.Add(new MessageItem
                { Property = nameof(ProductId), Code = PersonalizationResourceConstants.PURCHASE_SIGNAL_REFERENCE_INVALID });

        if (quantity <= 0)
            messages.Add(new MessageItem
                { Property = nameof(Quantity), Code = PersonalizationResourceConstants.PURCHASE_SIGNAL_QUANTITY_INVALID });

        if (unitPrice < 0)
            messages.Add(new MessageItem
                { Property = nameof(UnitPrice), Code = PersonalizationResourceConstants.PURCHASE_SIGNAL_UNIT_PRICE_INVALID });

        if (messages.Count > 0)
            return ResultDomain<PurchaseSignalItem>.Error(messages);

        return ResultDomain<PurchaseSignalItem>.Ok(new PurchaseSignalItem
        {
            ProductId = productId,
            Category = category,
            Brand = brand,
            Quantity = quantity,
            UnitPrice = unitPrice,
        });
    }
}