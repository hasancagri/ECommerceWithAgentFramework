namespace CustomNopCommerce.Domains.TierPrices;

/// <summary>
/// Kademeli fiyat — belirli bir üründe belirli adet ve üzeri alımda geçerli özel fiyat (ör. "10+ adet → 45 TL").
/// Pricing bounded context'inin aggregate kökü. ProductId + CustomerRoleId opak referanstır (Catalog/Identity).
/// nopCommerce TierPrice paritesi (StoreId çıkarıldı — çok-mağaza deferred). Pricing saf decimal kullanır.
/// </summary>
public class TierPrice : AggregateRoot
{
    public Guid ProductId { get; private set; }
    // Belirli bir müşteri rolüne özel kademe (ör. bayi); null = herkese. Rol Identity'nin — opak Id.
    public Guid? CustomerRoleId { get; private set; }
    public int Quantity { get; private set; }
    public decimal Price { get; private set; }
    public DateTime? StartDateUtc { get; private set; }
    public DateTime? EndDateUtc { get; private set; }

    private TierPrice() { }

    /// <summary>Yeni kademe oluşturur. Adet > 0 ve fiyat >= 0 guard'ı handler'da.</summary>
    /// <remarks>Handler: CreateTierPriceCommandHandler</remarks>
    public static TierPrice Create(Guid productId, Guid? customerRoleId, int quantity, decimal price,
        DateTime? startDateUtc, DateTime? endDateUtc)
    {
        return new TierPrice
        {
            ProductId = productId,
            CustomerRoleId = customerRoleId,
            Quantity = quantity,
            Price = price,
            StartDateUtc = startDateUtc,
            EndDateUtc = endDateUtc,
        };
    }

    /// <summary>Bu kademe verilen adet + zamanda geçerli mi? (adet ≥ eşik ve tarih penceresi içinde).
    /// Saf sorgu — durum değiştirmez.</summary>
    public bool AppliesTo(int orderQuantity, DateTime nowUtc)
    {
        if (orderQuantity < Quantity)
            return false;
        if (StartDateUtc is { } start && nowUtc < start)
            return false;
        if (EndDateUtc is { } end && nowUtc > end)
            return false;
        return true;
    }
}
