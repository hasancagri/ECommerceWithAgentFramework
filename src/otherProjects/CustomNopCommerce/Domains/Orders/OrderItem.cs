using CustomNopCommerce.Domains.Orders.ValueObjects;

namespace CustomNopCommerce.Domains.Orders;

/// <summary>
/// Sipariş kalemi — Order aggregate'inin child entity'si (kimliği var, bağımsız yaşamaz). Sipariş anında
/// dondurulmuş ürün kimliği + adet + birim fiyat taşır (ProductId opak — Catalog'a canlı bakılmaz; fiyat
/// sipariş anına ait snapshot'tır). nopCommerce OrderItem paritesi: InclTax/ExclTax ikizleri tek fiyata
/// indirgendi, download/rental/license alanları çıkarıldı. Satır toplamı TÜRETİLİR (saklanmaz).
/// </summary>
public class OrderItem
{
    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    public Money UnitPrice { get; private set; } = Money.Zero();
    public Money DiscountAmount { get; private set; } = Money.Zero();
    // İnsan-okur seçili attribute özeti (ör. "Renk: Kırmızı, Beden: M"); tipli seçim Basket/Catalog'da kalır.
    public string? AttributeDescription { get; private set; }

    private OrderItem() { }

    public static OrderItem Create(Guid productId, int quantity, Money unitPrice, Money discountAmount,
        string? attributeDescription)
    {
        return new OrderItem
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Quantity = quantity,
            UnitPrice = unitPrice,
            DiscountAmount = discountAmount,
            AttributeDescription = attributeDescription,
        };
    }

    /// <summary>Satır toplamı = birim fiyat × adet − indirim. Türetilir; ayrı alan yok.</summary>
    public Money LineTotal() => UnitPrice.Multiply(Quantity).Subtract(DiscountAmount);
}
