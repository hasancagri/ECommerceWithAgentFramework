namespace Procurement.Api.Domains.Suppliers.ValueObjects;

// Supplier aggregate'inin value object'leri — aggregate başına TEK dosya.

/// <summary>
/// Tedarikçi kategori adı → kanonik (Category, SubCategory) eşlemesi. Eşleme anahtarı normalize
/// saklanır (trim + lower) — feed'deki yazım farkları (boşluk/büyük harf) eşlemeyi bozmaz.
/// </summary>
public record CategoryMapping
{
    public string SupplierCategoryName { get; private init; } = default!;
    public string CanonicalCategory { get; private init; } = default!;
    public string CanonicalSubCategory { get; private init; } = default!;

    private CategoryMapping() { }

    public static CategoryMapping Create(string supplierCategoryName, string canonicalCategory, string canonicalSubCategory)
        => new()
        {
            SupplierCategoryName = Normalize(supplierCategoryName),
            CanonicalCategory = canonicalCategory,
            CanonicalSubCategory = canonicalSubCategory,
        };

    public static string Normalize(string raw) => raw.Trim().ToLowerInvariant();
}