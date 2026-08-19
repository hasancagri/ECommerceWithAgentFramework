namespace CustomNopCommerce.Domains.SpecificationAttributes;

/// <summary>
/// Spesifikasyon özniteliği — global, filtrelenebilir bir tanımlayıcı (ör. "Ekran Boyutu", "RAM",
/// "Materyal"). Variant'tan FARKI: müşteri seçmez, SKU üretmez, fiyat/stok etkilemez — yalnız ürünü
/// tanımlar + facet filtresi besler. Önceden tanımlı seçenekleri (<see cref="SpecificationAttributeOption"/>)
/// child koleksiyonda tutar. Opsiyonel gruba (SpecificationAttributeGroup) Id ile bağlanır.
/// nopCommerce SpecificationAttribute paritesi.
/// </summary>
public class SpecificationAttribute : AggregateRoot
{
    public string Name { get; private set; } = default!;
    public int DisplayOrder { get; private set; }
    public Guid? GroupId { get; private set; }

    private readonly List<SpecificationAttributeOption> _options = new();
    public IReadOnlyList<SpecificationAttributeOption> Options => _options;

    private SpecificationAttribute() { }

    /// <summary>Yeni spesifikasyon oluşturur. Ad zorunluluğu handler'da.</summary>
    /// <remarks>Handler: CreateSpecificationAttributeCommandHandler</remarks>
    public static SpecificationAttribute Create(string name, int displayOrder, Guid? groupId) =>
        new() { Name = name, DisplayOrder = displayOrder, GroupId = groupId };

    /// <summary>Spesifikasyon adını değiştirir.</summary>
    /// <remarks>Handler: (ileride UpdateSpecificationAttribute)</remarks>
    public ResultDomain Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(name), Code = CatalogResourceConstants.SPEC_NAME_REQUIRED });
        Name = name;
        return ResultDomain.Ok();
    }

    /// <summary>Önceden tanımlı bir seçenek ekler ve üretilen seçeneğin Id'sini döner.</summary>
    /// <remarks>Handler: AddSpecificationOptionCommandHandler</remarks>
    public ResultDomain<Guid> AddOption(string name, string? colorSquaresRgb, int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ResultDomain<Guid>.Error(new MessageItem
            { Property = nameof(name), Code = CatalogResourceConstants.SPEC_OPTION_NAME_REQUIRED });

        var option = SpecificationAttributeOption.Create(name, colorSquaresRgb, displayOrder);
        _options.Add(option);
        return ResultDomain<Guid>.Ok(option.Id);
    }

    /// <summary>Bir seçeneği Id ile çıkarır. Bulunamazsa hata döner.</summary>
    /// <remarks>Handler: (ileride RemoveSpecificationOption)</remarks>
    public ResultDomain RemoveOption(Guid optionId)
    {
        var option = _options.FirstOrDefault(o => o.Id == optionId);
        if (option is null)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(optionId), Code = CatalogResourceConstants.SPEC_OPTION_NOT_FOUND });
        _options.Remove(option);
        return ResultDomain.Ok();
    }

    /// <summary>Spesifikasyonu bir gruba taşır (null = grupsuz).</summary>
    /// <remarks>Handler: (ileride UpdateSpecificationAttribute)</remarks>
    public ResultDomain SetGroup(Guid? groupId)
    {
        GroupId = groupId;
        return ResultDomain.Ok();
    }
}
