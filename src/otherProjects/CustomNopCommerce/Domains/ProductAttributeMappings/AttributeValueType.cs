namespace CustomNopCommerce.Domains.ProductAttributeMappings;

/// <summary>
/// Bir attribute değerinin türü. Simple = düz seçenek (ör. "Kırmızı"); AssociatedToProduct = değer
/// başka bir ürüne bağlanır (grouped/bundle senaryosu — değer seçilince ilişkili ürün sepete girer).
/// </summary>
public enum AttributeValueType
{
    Simple = 0,
    AssociatedToProduct = 10,
}
