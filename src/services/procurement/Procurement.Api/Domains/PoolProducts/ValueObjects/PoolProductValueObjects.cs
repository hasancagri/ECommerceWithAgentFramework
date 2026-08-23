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

    // 043: ham tedarikçi attribute'ları (hash-diff girdisi) + handler'da kanonikleştirilmiş çiftler
    // (kategori deseni: eşleme handler'da, row temiz değeri taşır; eşlenemeyen ham anahtar yok sayılır).
    public IReadOnlyDictionary<string, string> RawAttributes { get; private init; } =
        new Dictionary<string, string>();
    public IReadOnlyList<SpecValue> CanonicalSpecs { get; private init; } = [];

    // 045: opsiyonel varyant ailesi kodu (aynı model üyeleri paylaşır); yok/boş = ailesiz.
    public string? FamilyCode { get; private init; }

    private ListingRow() { }

    public static ListingRow Create(
        string supplierSku, string name, string? description, string brand,
        string? rawCategoryName, string? canonicalCategory, string? canonicalSubCategory,
        decimal price, int stock, RowDimensions? dimensions,
        IReadOnlyDictionary<string, string>? rawAttributes = null,
        IReadOnlyList<SpecValue>? canonicalSpecs = null,
        string? familyCode = null)
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
            RawAttributes = rawAttributes ?? new Dictionary<string, string>(),
            CanonicalSpecs = canonicalSpecs ?? [],
            FamilyCode = string.IsNullOrWhiteSpace(familyCode) ? null : familyCode.Trim(),
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
            Dimensions?.Height.ToString(CultureInfo.InvariantCulture) ?? "",
            string.Join('\u001e', RawAttributes.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => $"{kv.Key}={kv.Value}")),
            string.Join('\u001e', CanonicalSpecs.OrderBy(x => x.Attribute, StringComparer.Ordinal)
                .Select(x => $"{x.Attribute}={x.Option}")),
            FamilyCode ?? "");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }
}

/// <summary>
/// Kanonik özellik çifti (043; ör. Renk=Siyah). Registry adlarıyla taşınır — event sözleşmesi AD.
/// Record value-eşitliği merge/filtre karşılaştırmalarını verir.
/// </summary>
public record SpecValue
{
    public string Attribute { get; private init; } = default!;
    public string Option { get; private init; } = default!;

    private SpecValue() { }

    public static SpecValue Create(string attribute, string option)
        => new() { Attribute = attribute, Option = option };
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
/// Barkodun kanonik içeriği (047: tek listing'ten kurulur — priority-merge/enrich yok).
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

    // 043: kanonik özellikler — IsComplete HESABINA GİRMEZ (FR-005: spec eksikliği yayını bloklamaz).
    public IReadOnlyList<SpecValue> Specs { get; private init; } = [];

    // 045: varyant ailesi kodu (opsiyonel); IsComplete'e GİRMEZ (ailesiz ürün yayınlanır).
    public string? FamilyCode { get; private init; }

    private CanonicalContent() { }

    public static CanonicalContent Create(
        string name, string description, string brand, string category, string subCategory,
        string sku, RowDimensions? dimensions, IReadOnlyList<SpecValue>? specs = null,
        string? familyCode = null)
        => new()
        {
            Name = name,
            Description = description,
            Brand = brand,
            Category = category,
            SubCategory = subCategory,
            Sku = sku,
            Dimensions = dimensions,
            Specs = specs ?? [],
            FamilyCode = familyCode,
        };

    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(Name) &&
        !string.IsNullOrWhiteSpace(Category) &&
        !string.IsNullOrWhiteSpace(Description);

    // Record eşitliği liste alanında referans karşılaştırır — Specs değer-eşitliğine çevrilir
    // (RebuildCanonical sıra-bağımsızlık sözleşmesi record kıyasıyla test edilir).
    public virtual bool Equals(CanonicalContent? other) =>
        other is not null &&
        Name == other.Name && Description == other.Description && Brand == other.Brand &&
        Category == other.Category && SubCategory == other.SubCategory && Sku == other.Sku &&
        Equals(Dimensions, other.Dimensions) && FamilyCode == other.FamilyCode &&
        Specs.OrderBy(x => x.Attribute).SequenceEqual(other.Specs.OrderBy(x => x.Attribute));

    public override int GetHashCode() =>
        HashCode.Combine(Name, Description, Brand, Category, SubCategory, Sku, Dimensions, FamilyCode);

    /// <summary>Kanonik içeriğin deterministik hash'i — tekrar-yayın kesici (PublishedContentHash).</summary>
    public string ComputeHash()
    {
        var payload = string.Join('\u001f',
            Name, Description, Brand, Category, SubCategory, Sku,
            Dimensions?.Weight.ToString(CultureInfo.InvariantCulture) ?? "",
            Dimensions?.Length.ToString(CultureInfo.InvariantCulture) ?? "",
            Dimensions?.Width.ToString(CultureInfo.InvariantCulture) ?? "",
            Dimensions?.Height.ToString(CultureInfo.InvariantCulture) ?? "",
            string.Join('\u001e', Specs.OrderBy(x => x.Attribute, StringComparer.Ordinal)
                .Select(x => $"{x.Attribute}={x.Option}")),
            FamilyCode ?? "");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }
}

/// <summary>
/// Barkodun güncel satış teklifi (fiyat + stok) — tek tedarikçi (buy-box söküldü, 047). Listing
/// delisted/yok ise stok 0, fiyat son bilinen. Record value-eşitliği yayın değişim tespitini verir.
/// </summary>
public record CurrentOffer
{
    public decimal Price { get; private init; }
    public int Stock { get; private init; }

    private CurrentOffer() { }

    public static CurrentOffer Create(decimal price, int stock)
        => new() { Price = price, Stock = stock };
}

/// <summary>
/// TryTakePublish sonucu (047): tek kanal. PublishCanonical=true ise CanonicalProductUpserted yayınlanır
/// (içerik + fiyat + stok birlikte). false = NoChange (değişimsiz/eksik). Buy-box olayı yok.
/// </summary>
public record PublishDecision
{
    public bool PublishCanonical { get; private init; }

    private PublishDecision() { }

    public static PublishDecision Publish() => new() { PublishCanonical = true };

    public static PublishDecision NoChange() => new();
}