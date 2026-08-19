namespace CustomNopCommerce.Domains.CheckoutAttributes;

/// <summary>
/// Checkout özniteliğinin müşteriye sunum tipi. ProductAttribute'un kontrol tipiyle benzer ama AYRI BC
/// (Ordering) — bilinçli olarak kendi küçük enum'ı (paylaşımlı domain modeli yok). nopCommerce paritesi.
/// </summary>
public enum CheckoutAttributeControlType
{
    DropdownList = 1,
    RadioList = 2,
    Checkboxes = 3,
    TextBox = 4,
    MultilineTextbox = 10,
    Datepicker = 20,
}
