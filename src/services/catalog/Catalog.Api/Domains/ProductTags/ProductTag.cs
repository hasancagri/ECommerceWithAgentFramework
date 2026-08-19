namespace Catalog.Api.Domains.ProductTags;

/// <summary>
/// Ürün etiketi — Catalog BC'nin küçük aggregate kökü (ör. "yeni-sezon", "outlet"). Ürünle çok-a-çok;
/// eşlemenin sahibi Product tarafıdır (<see cref="Products.Product.TagIds"/>). Etiket yalnız kendi
/// kimliğini + adını + SEO'sunu yönetir (nopCommerce ProductTag paritesi).
/// 040 K9: dış yüzey (endpoint/MCP) YOK; feed etiket vermez — yalnız domain + birim test.
/// </summary>
public class ProductTag : AggregateRoot
{
    public string Name { get; private set; } = default!;
    public SeoMetadata Seo { get; private set; } = SeoMetadata.Empty();

    private ProductTag()
    {
    }

    /// <summary>Yeni etiket oluşturur. Ad zorunluluğu handler'da denetlenir (factory düz aggregate döner).</summary>
    /// <remarks>Handler: (henüz yok — K9: besleyen akış 040'ta bağlanmadı)</remarks>
    public static ProductTag Create(string name)
    {
        return new ProductTag { Name = name };
    }

    /// <summary>Etiket adını değiştirir.</summary>
    /// <remarks>Handler: (henüz yok — K9: besleyen akış 040'ta bağlanmadı)</remarks>
    public ResultDomain Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(name), Code = CatalogResourceConstants.TAG_NAME_REQUIRED });
        Name = name;
        return ResultDomain.Ok();
    }
}