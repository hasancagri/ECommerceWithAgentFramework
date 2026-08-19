namespace CustomNopCommerce.Domains.Shipments;

/// <summary>
/// Sevkiyat — bir siparişin (ya da parçasının) fiziksel gönderisi. Shipping bounded context'inin aggregate
/// kökü. OrderId opak referanstır (Ordering BC'ye canlı bakılmaz). Zengin aggregate dersi: SEVK YAŞAM
/// DÖNGÜSÜ invariant'ı — takip numarası olmadan kargolanamaz; kargolanmadan teslim edilemez (durum sırası
/// tarih damgalarıyla korunur). Kalemler child. nopCommerce Shipment + ShipmentItem paritesi.
/// </summary>
public class Shipment : AggregateRoot
{
    public Guid OrderId { get; private set; }
    public string? TrackingNumber { get; private set; }
    public decimal? TotalWeight { get; private set; }
    public DateTime? ShippedDateUtc { get; private set; }
    public DateTime? DeliveryDateUtc { get; private set; }

    private readonly List<ShipmentItem> _items = new();
    public IReadOnlyList<ShipmentItem> Items => _items;

    private Shipment() { }

    /// <summary>Bir sipariş için sevkiyat oluşturur (henüz kargolanmadı). Kalem boşluğu handler'da denetlenir.</summary>
    /// <remarks>Handler: CreateShipmentCommandHandler</remarks>
    public static Shipment Create(Guid orderId, decimal? totalWeight, IEnumerable<ShipmentItem> items)
    {
        var shipment = new Shipment { OrderId = orderId, TotalWeight = totalWeight };
        shipment._items.AddRange(items);
        return shipment;
    }

    /// <summary>Takip numarasını atar/günceller.</summary>
    /// <remarks>Handler: SetTrackingNumberCommandHandler</remarks>
    public ResultDomain SetTrackingNumber(string trackingNumber)
    {
        if (string.IsNullOrWhiteSpace(trackingNumber))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(trackingNumber), Code = ShippingResourceConstants.SHIPMENT_TRACKING_REQUIRED });
        TrackingNumber = trackingNumber;
        return ResultDomain.Ok();
    }

    /// <summary>Sevkiyatı kargolandı işaretler. Takip numarası ZORUNLU; zaten kargolanmışsa reddedilir (invariant).</summary>
    /// <remarks>Handler: MarkShipmentShippedCommandHandler</remarks>
    public ResultDomain MarkAsShipped(DateTime shippedAtUtc)
    {
        if (ShippedDateUtc is not null)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(ShippedDateUtc), Code = ShippingResourceConstants.SHIPMENT_ALREADY_SHIPPED });
        if (string.IsNullOrWhiteSpace(TrackingNumber))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(TrackingNumber), Code = ShippingResourceConstants.SHIPMENT_TRACKING_REQUIRED });
        ShippedDateUtc = shippedAtUtc;
        return ResultDomain.Ok();
    }

    /// <summary>Sevkiyatı teslim edildi işaretler. Önce kargolanmış OLMALI (invariant: sıra korunur).</summary>
    /// <remarks>Handler: MarkShipmentDeliveredCommandHandler</remarks>
    public ResultDomain MarkAsDelivered(DateTime deliveredAtUtc)
    {
        if (ShippedDateUtc is null)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(ShippedDateUtc), Code = ShippingResourceConstants.SHIPMENT_NOT_SHIPPED_YET });
        DeliveryDateUtc = deliveredAtUtc;
        return ResultDomain.Ok();
    }
}
