namespace Procurement.Api.Domains.PoolProducts.Entities;

/// <summary>
/// (Tedarikçi × barkod) ham feed satırı — PoolProduct aggregate'ine ait entity (base sınıf almaz).
/// SupplierPriority denormalize taşınır: merge + buy-box saf kalır (Supplier lookup'sız, R1).
/// Feed'den düşen satır silinmez, IsDelisted işaretlenir (FR-006) — yarıştan ve merge'den çıkar.
/// </summary>
public class SupplierListing
{
    public Guid SupplierId { get; private set; }
    public int SupplierPriority { get; private set; }
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
    public string ContentHash { get; private set; } = default!;
    public bool IsDelisted { get; private set; }
    public DateTime LastSeenUtc { get; private set; }

    private SupplierListing()
    {
    }

    public static SupplierListing Create(Guid supplierId, int supplierPriority, ListingRow row)
    {
        var listing = new SupplierListing { SupplierId = supplierId };
        listing.Refresh(supplierPriority, row);
        return listing;
    }

    /// <summary>Satırı feed'deki güncel haliyle ezer; delist işaretini kaldırır (yeniden listelendi).</summary>
    public void Refresh(int supplierPriority, ListingRow row)
    {
        SupplierPriority = supplierPriority;
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
        ContentHash = row.ComputeContentHash();
        IsDelisted = false;
        LastSeenUtc = DateTime.UtcNow;
    }

    /// <summary>Değişmeyen satırın görülme zamanını tazeler.</summary>
    public void Touch() => LastSeenUtc = DateTime.UtcNow;

    /// <summary>Feed'de görünmeyen satırı yarıştan çıkarır (silme yok — FR-006).</summary>
    public void Delist() => IsDelisted = true;
}