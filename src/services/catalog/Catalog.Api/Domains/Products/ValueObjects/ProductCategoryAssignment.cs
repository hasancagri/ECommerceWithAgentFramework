namespace Catalog.Api.Domains.Products.ValueObjects;

/// <summary>
/// Ürünün bir kategoriye atanması (nopCommerce ProductCategory eşlemesi). Product ile Category
/// çok-a-çok; eşleme öne-çıkan (featured) bayrağı + sıralama taşır. Product aggregate'inin child'ı,
/// mutasyon yalnız Product.AssignToCategory/RemoveFromCategory üzerinden geçer.
/// 040 K4: model çoklu atama taşır; ingestion tek kategori atar, ilk atama = primary (event'e gider).
/// </summary>
public record ProductCategoryAssignment
{
    public Guid CategoryId { get; private init; }
    public bool IsFeatured { get; private init; }
    public int DisplayOrder { get; private init; }

    private ProductCategoryAssignment() { }

    public static ProductCategoryAssignment Create(Guid categoryId, bool isFeatured, int displayOrder) =>
        new() { CategoryId = categoryId, IsFeatured = isFeatured, DisplayOrder = displayOrder };
}