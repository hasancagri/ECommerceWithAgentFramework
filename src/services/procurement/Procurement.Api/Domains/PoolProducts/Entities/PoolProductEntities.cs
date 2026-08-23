namespace Procurement.Api.Domains.PoolProducts.Entities;

/// <summary>
/// (Tedarikçi × barkod) ham feed satırı — PoolProduct aggregate'ine ait entity (base sınıf almaz).
/// 047: barkod global tekil → barkod-başı TEK tedarikçi. Buy-box/priority-merge söküldü; SupplierPriority
/// ve satır-düzeyi ContentHash kaldırıldı (idempotency tek noktada = TryTakePublish publish-gate).
/// Feed'den düşen satır silinmez, IsDelisted işaretlenir (FR-006) — kanonikten çıkar (stok 0).
/// </summary>
public class SupplierListing
{
    public Guid SupplierId { get; private set; }
    public string SupplierSku { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public string Brand { get; private set; } = default!;
    public string? RawCategoryName { get; private set; }
    public string? CanonicalCategory { get; private set; }
    public string? CanonicalSubCategory { get; private set; }
    public decimal Price { get; private set; }
    public int Stock { get; private set; }
    public RowDimensions? Dimensions { get; private set; }

    // 043: ham attribute'lar (debug/yeniden-eşleme) + kanonikleşmiş çiftler (merge girdisi).
    public Dictionary<string, string> RawAttributes { get; private set; } = new();
    public List<SpecValue> CanonicalSpecs { get; private set; } = [];

    // 045: varyant ailesi kodu (opsiyonel; null = ailesiz).
    public string? FamilyCode { get; private set; }

    public bool IsDelisted { get; private set; }
    public DateTime LastSeenUtc { get; private set; }

    private SupplierListing()
    {
    }

    public static SupplierListing Create(Guid supplierId, ListingRow row)
    {
        var listing = new SupplierListing { SupplierId = supplierId };
        listing.Refresh(row);
        return listing;
    }

    /// <summary>Satırı feed'deki güncel haliyle koşulsuz ezer; delist işaretini kaldırır (yeniden listelendi).</summary>
    public void Refresh(ListingRow row)
    {
        SupplierSku = row.SupplierSku;
        Name = row.Name;
        Description = row.Description;
        Brand = row.Brand;
        RawCategoryName = row.RawCategoryName;
        CanonicalCategory = row.CanonicalCategory;
        CanonicalSubCategory = row.CanonicalSubCategory;
        Price = row.Price;
        Stock = row.Stock;
        Dimensions = row.Dimensions;
        RawAttributes = new Dictionary<string, string>(row.RawAttributes);
        CanonicalSpecs = row.CanonicalSpecs.ToList();
        FamilyCode = row.FamilyCode;
        IsDelisted = false;
        LastSeenUtc = DateTime.UtcNow;
    }

    /// <summary>Feed'de görünmeyen satırı işaretler (silme yok — FR-006).</summary>
    public void Delist() => IsDelisted = true;
}
