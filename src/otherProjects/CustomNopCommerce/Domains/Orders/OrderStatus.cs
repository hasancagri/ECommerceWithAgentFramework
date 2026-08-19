namespace CustomNopCommerce.Domains.Orders;

/// <summary>Siparişin genel yaşam döngüsü durumu. nopCommerce OrderStatus paritesi.</summary>
public enum OrderStatus
{
    Pending = 10,
    Processing = 20,
    Complete = 30,
    Cancelled = 40,
}
