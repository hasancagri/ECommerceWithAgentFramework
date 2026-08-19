namespace CustomNopCommerce.Domains.ProductSpecificationAttributes;

/// <summary>
/// Bir spesifikasyonun bir ürüne atanması (ör. "iPhone 15" → Ekran Boyutu = 6.1"). Aggregate kökü;
/// ProductId + SpecificationAttributeId ile referans verir. Değer türe bağlıdır (invariant, handler'da
/// denetlenir): <see cref="SpecificationAttributeType.Option"/> ise <see cref="SpecificationAttributeOptionId"/>
/// zorunlu; CustomText/CustomHtmlText/Hyperlink ise <see cref="CustomValue"/> zorunlu.
/// <see cref="AllowFiltering"/> bu spec'in faceted filtreye girip girmeyeceğini belirler.
/// nopCommerce ProductSpecificationAttribute paritesi.
/// </summary>
public class ProductSpecificationAttribute : AggregateRoot
{
    public Guid ProductId { get; private set; }
    public Guid SpecificationAttributeId { get; private set; }
    public SpecificationAttributeType Type { get; private set; }
    // Option türünde geçerli seçeneğin Id'si; custom/hyperlink türlerinde null.
    public Guid? SpecificationAttributeOptionId { get; private set; }
    // Custom/hyperlink türlerinde serbest değer; Option türünde null.
    public string? CustomValue { get; private set; }
    public bool AllowFiltering { get; private set; }
    public bool ShowOnProductPage { get; private set; }
    public int DisplayOrder { get; private set; }

    private ProductSpecificationAttribute() { }

    /// <summary>Spesifikasyonu ürüne atar. Türe göre alan zorunluluğu (Option→optionId, custom→value)
    /// handler'da denetlenir; factory düz aggregate döner.</summary>
    /// <remarks>Handler: AssignSpecificationToProductCommandHandler</remarks>
    public static ProductSpecificationAttribute Create(Guid productId, Guid specificationAttributeId,
        SpecificationAttributeType type, Guid? optionId, string? customValue,
        bool allowFiltering, bool showOnProductPage, int displayOrder)
    {
        return new ProductSpecificationAttribute
        {
            ProductId = productId,
            SpecificationAttributeId = specificationAttributeId,
            Type = type,
            SpecificationAttributeOptionId = optionId,
            CustomValue = customValue,
            AllowFiltering = allowFiltering,
            ShowOnProductPage = showOnProductPage,
            DisplayOrder = displayOrder,
        };
    }

    /// <summary>Bu spec'in faceted filtreye girip girmeyeceğini değiştirir.</summary>
    /// <remarks>Handler: (ileride UpdateProductSpecification)</remarks>
    public ResultDomain SetFiltering(bool allowFiltering)
    {
        AllowFiltering = allowFiltering;
        return ResultDomain.Ok();
    }

    /// <summary>Bu spec'in ürün sayfasında gösterilip gösterilmeyeceğini değiştirir.</summary>
    /// <remarks>Handler: (ileride UpdateProductSpecification)</remarks>
    public ResultDomain SetShowOnProductPage(bool show)
    {
        ShowOnProductPage = show;
        return ResultDomain.Ok();
    }
}
