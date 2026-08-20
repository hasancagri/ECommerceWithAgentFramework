using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Procurement.Api.Domains.PoolProducts.ValueObjects;

// PoolProduct aggregate'inin value object'leri — aggregate başına TEK dosya. VO'da helper serbest.

/// <summary>
/// Feed'den gelen tek ham satırın domain'e giriş yüzü (kategori eşlemesi handler'da çözülmüş halde).
/// İçerik hash'i buradan üretilir — satır düzeyi diff'in temeli (FR-006).
/// </summary>
public record ListingRow
{
    public string SupplierSku { get; private init; } = default!;
    public string Name { get; private init; } = default!;
    public string? Description { get; private init; }
    public string Brand { get; private init; } = default!;
    public string? RawCategoryName { get; private init; }
    public string? CanonicalCategory { get; private init; }
    public string? CanonicalSubCategory { get; private init; }
    public decimal Price { get; private init; }
    public int Stock { get; private init; }
    public RowDimensions? Dimensions { get; private init; }

    private ListingRow() { }

    public static ListingRow Create(
        string supplierSku, string name, string? description, string brand,
        string? rawCategoryName, string? canonicalCategory, string? canonicalSubCategory,
        decimal price, int stock, RowDimensions? dimensions)
        => new()
        {
            SupplierSku = supplierSku,
            Name = name,
            Description = description,
            Brand = brand,
            RawCategoryName = rawCategoryName,
            CanonicalCategory = canonicalCategory,
            CanonicalSubCategory = canonicalSubCategory,
            Price = price,
            Stock = stock,
            Dimensions = dimensions,
        };

    /// <summary>Satırın deterministik içerik hash'i (SHA256, invariant-culture). Fiyat + stok dahil:
    /// herhangi bir alan değişimi satırı "değişti" sayar.</summary>
    public string ComputeContentHash()
    {
        var payload = string.Join('\u001f',
            SupplierSku, Name, Description ?? "", Brand, RawCategoryName ?? "",
            Price.ToString(CultureInfo.InvariantCulture),
            Stock.ToString(CultureInfo.InvariantCulture),
            Dimensions?.Weight.ToString(CultureInfo.InvariantCulture) ?? "",
            Dimensions?.Length.ToString(CultureInfo.InvariantCulture) ?? "",
            Dimensions?.Width.ToString(CultureInfo.InvariantCulture) ?? "",
            Dimensions?.Height.ToString(CultureInfo.InvariantCulture) ?? "");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }
}

/// <summary>
/// Fiziksel ölçüler (feed'den; AI ASLA üretmez — FR-010). Catalog'a event'te düz alanlar gider;
/// 0 = bilinmiyor (Catalog ProductDimensions.Empty ile yaşar, yayını bloklamaz).
/// </summary>
public record RowDimensions
{
    public decimal Weight { get; private init; }
    public decimal Length { get; private init; }
    public decimal Width { get; private init; }
    public decimal Height { get; private init; }

    private RowDimensions() { }

    /// <summary>Negatif ölçü varsa null döner (guard çağıranda).</summary>
    public static RowDimensions? Create(decimal weight, decimal length, decimal width, decimal height)
    {
        if (weight < 0 || length < 0 || width < 0 || height < 0)
            return null;
        return new RowDimensions { Weight = weight, Length = length, Width = width, Height = height };
    }
}

/// <summary>
/// Barkodun birleştirilmiş kanonik içeriği (R9 Priority-merge + enrich overlay sonucu).
/// IsComplete = Name + Description + Category dolu; ölçü/Sku eksikliği yayını BLOKLAMAZ (FR-010).
/// </summary>
public record CanonicalContent
{
    public string Name { get; private init; } = string.Empty;
    public string Description { get; private init; } = string.Empty;
    public string Brand { get; private init; } = string.Empty;
    public string Category { get; private init; } = string.Empty;
    public string SubCategory { get; private init; } = string.Empty;
    public string Sku { get; private init; } = string.Empty;
    public RowDimensions? Dimensions { get; private init; }

    private CanonicalContent() { }

    public static CanonicalContent Create(
        string name, string description, string brand, string category, string subCategory,
        string sku, RowDimensions? dimensions)
        => new()
        {
            Name = name,
            Description = description,
            Brand = brand,
            Category = category,
            SubCategory = subCategory,
            Sku = sku,
            Dimensions = dimensions,
        };

    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(Name) &&
        !string.IsNullOrWhiteSpace(Description) &&
        !string.IsNullOrWhiteSpace(Category);

    /// <summary>Kanonik içeriğin deterministik hash'i — tekrar-yayın kesici (PublishedContentHash).</summary>
    public string ComputeHash()
    {
        var payload = string.Join('\u001f',
            Name, Description, Brand, Category, SubCategory, Sku,
            Dimensions?.Weight.ToString(CultureInfo.InvariantCulture) ?? "",
            Dimensions?.Length.ToString(CultureInfo.InvariantCulture) ?? "",
            Dimensions?.Width.ToString(CultureInfo.InvariantCulture) ?? "",
            Dimensions?.Height.ToString(CultureInfo.InvariantCulture) ?? "");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }
}

/// <summary>
/// Buy-box kararı: stok>0 en ucuz offer'ın kimlik + fiyat + stoğu. Kazanan yoksa SupplierId null,
/// Stock 0, Price son bilinen (vitrinde fiyat kalır). Record value-eşitliği değişim tespitini verir.
/// </summary>
public record BuyBoxDecision
{
    public Guid? SupplierId { get; private init; }
    public decimal? Price { get; private init; }
    public int Stock { get; private init; }

    private BuyBoxDecision() { }

    public static BuyBoxDecision Winner(Guid supplierId, decimal price, int stock)
        => new() { SupplierId = supplierId, Price = price, Stock = stock };

    public static BuyBoxDecision NoWinner(decimal? lastKnownPrice)
        => new() { SupplierId = null, Price = lastKnownPrice, Stock = 0 };
}

/// <summary>
/// Enrich agent çıktısı + kaynak hash'i (cache: aynı eksik-girdi için AI tekrar çağrılmaz — FR-009).
/// Yalnız İÇERİK alanları taşır; barkod/ölçü/fiyat/stok yapısal olarak taşınamaz (FR-010).
/// </summary>
public record EnrichmentResult
{
    public string SourceHash { get; private init; } = default!;
    public string? Description { get; private init; }
    public string? Category { get; private init; }
    public string? SubCategory { get; private init; }
    public DateTime EnrichedAtUtc { get; private init; }

    private EnrichmentResult() { }

    public static EnrichmentResult Create(string sourceHash, string? description, string? category, string? subCategory)
        => new()
        {
            SourceHash = sourceHash,
            Description = description,
            Category = category,
            SubCategory = subCategory,
            EnrichedAtUtc = DateTime.UtcNow,
        };
}

/// <summary>
/// Kanonik taksonomi çifti (enrich'in seçim listesi — R3: Procurement kendi kopyasını taşır).
/// </summary>
public record CanonicalCategoryPair
{
    public string Category { get; private init; } = default!;
    public string SubCategory { get; private init; } = default!;

    private CanonicalCategoryPair() { }

    public static CanonicalCategoryPair Create(string category, string subCategory)
        => new() { Category = category, SubCategory = subCategory };
}

/// <summary>
/// TryTakePublish sonucu: hangi event'lerin yayınlanacağı. İkisi de false = NoChange.
/// Kalıcı değildir (dönüş değeri); Canonical değişimi CanonicalProductUpserted'ı, buy-box değişimi
/// BuyBoxChanged'i tetikler — ikisi aynı pull'da birlikte doğabilir.
/// </summary>
public record PublishDecision
{
    public bool PublishCanonical { get; private init; }
    public bool PublishBuyBox { get; private init; }

    private PublishDecision() { }

    public static PublishDecision Create(bool publishCanonical, bool publishBuyBox)
        => new() { PublishCanonical = publishCanonical, PublishBuyBox = publishBuyBox };

    public static PublishDecision NoChange() => new();
}