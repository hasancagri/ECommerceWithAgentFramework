using CustomNopCommerce.Domains.Orders.ValueObjects;

namespace CustomNopCommerce.Domains.Orders;

/// <summary>
/// Sipariş — Ordering bounded context'inin kök aggregate'i. nopCommerce'in ~65 alanlık god-Order'ı burada
/// SERT biçimde bölündü:
/// - PAN/CVV/kart alanları (CardNumber, CardCvv2, Masked...) TAMAMEN SİLİNDİ — asla saklanmaz (PCI).
/// - Payment transaction alanları (Authorization/Capture...) → Payment BC; burada yalnız <see cref="PaymentStatus"/>.
/// - Kargo method/rate → Shipping BC; burada yalnız <see cref="ShippingStatus"/>.
/// - Vergi InclTax/ExclTax ikizleri → tek tutara indirgendi (Tax BC hesaplar); bkz. <see cref="OrderTotals"/>.
/// - Affiliate → Affiliates, RewardPoints → Loyalty, dil/store → deferred.
/// Kalan: müşteri + adres Id'leri (opak) + 3 statü + para özeti + kalemler + notlar. CustomerId/adresler
/// opak referanstır (Customer BC'ye canlı bakılmaz). Kalemler + notlar child; mutasyon yalnız kökten.
/// </summary>
public class Order : AggregateRoot
{
    public Guid CustomerId { get; private set; }
    public string CustomOrderNumber { get; private set; } = default!;

    public OrderStatus OrderStatus { get; private set; }
    public PaymentStatus PaymentStatus { get; private set; }
    public ShippingStatus ShippingStatus { get; private set; }

    public Guid BillingAddressId { get; private set; }
    public Guid? ShippingAddressId { get; private set; }
    public bool PickupInStore { get; private set; }

    public string CurrencyCode { get; private set; } = "TRY";
    public OrderTotals Totals { get; private set; } = default!;
    public DateTime? PaidDateUtc { get; private set; }

    private readonly List<OrderItem> _items = new();
    public IReadOnlyList<OrderItem> Items => _items;

    private readonly List<OrderNote> _notes = new();
    public IReadOnlyList<OrderNote> Notes => _notes;

    // Türetilmiş: kalem satır toplamlarının toplamı (indirim/kargo/vergi hariç ham mal bedeli).
    public Money ItemsSubtotal()
    {
        var sum = Money.Zero(CurrencyCode);
        foreach (var item in _items)
            sum = sum.Add(item.LineTotal());
        return sum;
    }

    private Order() { }

    /// <summary>Sipariş oluşturur (Pending doğar). Kalem boşluğu + adet guard'ı handler'da; kalemler +
    /// hesaplanmış totaller checkout pipeline'ından (Pricing/Tax/Shipping) gelir.</summary>
    /// <remarks>Handler: PlaceOrderCommandHandler</remarks>
    public static Order Create(Guid customerId, string customOrderNumber, Guid billingAddressId,
        Guid? shippingAddressId, bool pickupInStore, string currencyCode,
        IEnumerable<OrderItem> items, OrderTotals totals)
    {
        var order = new Order
        {
            CustomerId = customerId,
            CustomOrderNumber = customOrderNumber,
            OrderStatus = OrderStatus.Pending,
            PaymentStatus = PaymentStatus.Pending,
            ShippingStatus = pickupInStore ? ShippingStatus.ShippingNotRequired : ShippingStatus.NotYetShipped,
            BillingAddressId = billingAddressId,
            ShippingAddressId = shippingAddressId,
            PickupInStore = pickupInStore,
            CurrencyCode = currencyCode,
            Totals = totals,
        };
        order._items.AddRange(items);
        return order;
    }

    /// <summary>Ödemeyi tamamlanmış işaretler (Payment BC bildirimiyle). Ödeme tarihini damgalar.</summary>
    /// <remarks>Handler: MarkOrderAsPaidCommandHandler</remarks>
    public ResultDomain MarkAsPaid(DateTime paidAtUtc)
    {
        PaymentStatus = PaymentStatus.Paid;
        PaidDateUtc = paidAtUtc;
        if (OrderStatus == OrderStatus.Pending)
            OrderStatus = OrderStatus.Processing;
        return ResultDomain.Ok();
    }

    /// <summary>Siparişi kargolanmış işaretler. Kargo gerekmiyorsa reddedilir.</summary>
    /// <remarks>Handler: (ileride ShipOrder)</remarks>
    public ResultDomain Ship()
    {
        if (ShippingStatus == ShippingStatus.ShippingNotRequired)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(ShippingStatus), Code = OrderingResourceConstants.RECORD_NOT_FOUND });
        ShippingStatus = ShippingStatus.Shipped;
        return ResultDomain.Ok();
    }

    /// <summary>Siparişi teslim edilmiş işaretler.</summary>
    /// <remarks>Handler: (ileride DeliverOrder)</remarks>
    public ResultDomain Deliver()
    {
        ShippingStatus = ShippingStatus.Delivered;
        return ResultDomain.Ok();
    }

    /// <summary>Siparişi tamamlar (kapatır).</summary>
    /// <remarks>Handler: (ileride CompleteOrder)</remarks>
    public ResultDomain Complete()
    {
        OrderStatus = OrderStatus.Complete;
        return ResultDomain.Ok();
    }

    /// <summary>Siparişi iptal eder. Zaten iptal veya tamamlanmış sipariş iptal EDİLEMEZ (invariant).</summary>
    /// <remarks>Handler: CancelOrderCommandHandler</remarks>
    public ResultDomain Cancel()
    {
        if (OrderStatus == OrderStatus.Cancelled)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(OrderStatus), Code = OrderingResourceConstants.ORDER_ALREADY_CANCELLED });
        if (OrderStatus == OrderStatus.Complete)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(OrderStatus), Code = OrderingResourceConstants.ORDER_CANNOT_CANCEL_COMPLETE });
        OrderStatus = OrderStatus.Cancelled;
        return ResultDomain.Ok();
    }

    /// <summary>Siparişe not ekler. Boş not reddedilir.</summary>
    /// <remarks>Handler: AddOrderNoteCommandHandler</remarks>
    public ResultDomain AddNote(string note, bool displayToCustomer, DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(note))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(note), Code = OrderingResourceConstants.ORDER_NOTE_EMPTY });
        _notes.Add(OrderNote.Create(note, displayToCustomer, createdAtUtc));
        return ResultDomain.Ok();
    }
}
