namespace CustomNopCommerce.Domains.Orders;

/// <summary>Siparişin ödeme durumu. nopCommerce PaymentStatus paritesi. Gerçek çekim/iade Payment BC'de;
/// burada yalnız durum yansıtılır (transaction detayları + kart alanları ALINMADI — PCI).</summary>
public enum PaymentStatus
{
    Pending = 10,
    Authorized = 20,
    Paid = 30,
    PartiallyRefunded = 35,
    Refunded = 40,
    Voided = 50,
}
