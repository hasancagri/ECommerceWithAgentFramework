namespace CustomNopCommerce.Domains.SpecificationAttributeGroups;

/// <summary>
/// Spesifikasyon gruplaması (ör. "Teknik Özellikler", "Fiziksel"). Ürün sayfasında spec'leri başlık
/// altında toplar. Küçük aggregate kökü. nopCommerce SpecificationAttributeGroup paritesi.
/// </summary>
public class SpecificationAttributeGroup : AggregateRoot
{
    public string Name { get; private set; } = default!;
    public int DisplayOrder { get; private set; }

    private SpecificationAttributeGroup() { }

    /// <summary>Yeni grup oluşturur. Ad zorunluluğu handler'da.</summary>
    /// <remarks>Handler: CreateSpecificationAttributeGroupCommandHandler</remarks>
    public static SpecificationAttributeGroup Create(string name, int displayOrder) =>
        new() { Name = name, DisplayOrder = displayOrder };

    /// <summary>Grup adını değiştirir.</summary>
    /// <remarks>Handler: (ileride UpdateSpecificationAttributeGroup)</remarks>
    public ResultDomain Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(name), Code = CatalogResourceConstants.SPEC_GROUP_NAME_REQUIRED });
        Name = name;
        return ResultDomain.Ok();
    }
}
