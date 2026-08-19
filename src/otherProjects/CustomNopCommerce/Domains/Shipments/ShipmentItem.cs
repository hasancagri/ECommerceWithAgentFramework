namespace CustomNopCommerce.Domains.Shipments;

/// <summary>
/// Sevkiyat kalemi — Shipment aggregate'inin child entity'si. Hangi sipariş kaleminden kaç adet, hangi
/// depodan çıktığını taşır. OrderItemId (Ordering BC) + WarehouseId (Shipping) opak referans.
/// nopCommerce ShipmentItem paritesi.
/// </summary>
public class ShipmentItem
{
    public Guid Id { get; private set; }
    public Guid OrderItemId { get; private set; }
    public int Quantity { get; private set; }
    public Guid WarehouseId { get; private set; }

    private ShipmentItem() { }

    public static ShipmentItem Create(Guid orderItemId, int quantity, Guid warehouseId) =>
        new() { Id = Guid.NewGuid(), OrderItemId = orderItemId, Quantity = quantity, WarehouseId = warehouseId };
}
