using CustomNopCommerce.Domains.Products.ValueObjects;

namespace CustomNopCommerce.Domains.ProductAttributeCombinations;

/// <summary>
/// Bir ürünün satılabilir somut varyantı — seçilen attribute değerlerinin (ör. Renk=Kırmızı + Beden=M)
/// belirli bir kombinasyonu. Kendi SKU'su + isteğe bağlı ezici fiyatı vardır. Aggregate kökü; ProductId +
/// seçilen değer Id'leri ile referans verir. nopCommerce ProductAttributeCombination paritesi, AMA
/// stok alanları ÇIKARILDI: varyant stoğu Stock BC'nin işidir (SKU anahtarıyla), burada tutulmaz.
/// AttributesXml yerine tipli <see cref="SelectedValueIds"/> kullanılır.
/// </summary>
public class ProductAttributeCombination : AggregateRoot
{
    public Guid ProductId { get; private set; }
    public string Sku { get; private set; } = default!;
    public string? Gtin { get; private set; }
    public string? ManufacturerPartNumber { get; private set; }
    // Ezici fiyat verilmişse taban fiyat + ayarlamalar yerine bu kullanılır (null = ürün fiyatını kullan).
    public Money? OverriddenPrice { get; private set; }

    private readonly List<Guid> _selectedValueIds = new();
    public IReadOnlyList<Guid> SelectedValueIds => _selectedValueIds;

    private ProductAttributeCombination() { }

    /// <summary>Seçilen değerlerle yeni bir varyant oluşturur. SKU + en az bir değer zorunluluğu handler'da.</summary>
    /// <remarks>Handler: CreateProductAttributeCombinationCommandHandler</remarks>
    public static ProductAttributeCombination Create(Guid productId, string sku, string? gtin,
        string? manufacturerPartNumber, IEnumerable<Guid> selectedValueIds)
    {
        var combination = new ProductAttributeCombination
        {
            ProductId = productId,
            Sku = sku,
            Gtin = gtin,
            ManufacturerPartNumber = manufacturerPartNumber,
        };
        combination._selectedValueIds.AddRange(selectedValueIds);
        return combination;
    }

    /// <summary>Varyanta ezici fiyat atar (taban fiyatı geçersiz kılar).</summary>
    /// <remarks>Handler: (ileride UpdateProductAttributeCombination)</remarks>
    public ResultDomain OverridePrice(Money price)
    {
        OverriddenPrice = price;
        return ResultDomain.Ok();
    }

    /// <summary>Ezici fiyatı kaldırır — varyant tekrar ürün taban fiyatını kullanır.</summary>
    /// <remarks>Handler: (ileride UpdateProductAttributeCombination)</remarks>
    public ResultDomain ClearPriceOverride()
    {
        OverriddenPrice = null;
        return ResultDomain.Ok();
    }
}
