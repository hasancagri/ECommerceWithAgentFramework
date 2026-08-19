namespace CustomNopCommerce.Domains.Orders;

/// <summary>Siparişin kargo durumu. nopCommerce ShippingStatus paritesi. Kargo hesap/rate Shipping BC'de;
/// burada yalnız durum yansıtılır.</summary>
public enum ShippingStatus
{
    ShippingNotRequired = 10,
    NotYetShipped = 20,
    PartiallyShipped = 25,
    Shipped = 30,
    Delivered = 40,
}
