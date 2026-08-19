namespace CustomNopCommerce.Domains.ProductSpecificationAttributes;

/// <summary>
/// Bir ürüne atanan spesifikasyonun değer türü. Option = önceden tanımlı seçenek (filtrelenebilir);
/// CustomText/CustomHtmlText = ürüne özgü serbest değer; Hyperlink = bağlantı. nopCommerce paritesi.
/// Yalnız Option türü faceted filtrelemeye girer (seçenek Id'si ortak).
/// </summary>
public enum SpecificationAttributeType
{
    Option = 0,
    CustomText = 10,
    CustomHtmlText = 20,
    Hyperlink = 30,
}
