namespace Procurement.Api.Domains.Suppliers;

/// <summary>
/// Tedarikçi — Procurement BC aggregate'i. Seed'le doğar (statik kimlik, R2): benzersiz Code feed
/// ucu/adapter anahtarı, benzersiz Priority tie-break + merge önceliğidir (düşük kazanır; A=1, B=2).
/// Kategori eşleme tablosu tedarikçiye özgü adları kanonik (Category, SubCategory) çiftine çevirir;
/// yeni kategori feed'den DOĞMAZ (FR-007) — eşlenemeyen ad enrich'e düşer.
/// </summary>
public class Supplier : AggregateRoot
{
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public int Priority { get; private set; }

    private readonly List<CategoryMapping> _categoryMappings = new();
    public IReadOnlyList<CategoryMapping> CategoryMappings => _categoryMappings;

    private Supplier()
    {
    }

    // JasperFxIgnore: statik Create fabrikası event-sourcing evolver konvansiyonuyla çakışır;
    // bu bir domain fabrikasıdır, projection değil (source generator'ı devre dışı bırakır).
    /// <summary>Yeni tedarikçi oluşturur. Boş kod/ad ve pozitif olmayan öncelik reddedilir.</summary>
    /// <remarks>Handler: ProcurementSeedHostedService</remarks>
    [JasperFx.Core.JasperFxIgnore]
    public static ResultDomain<Supplier> Create(string code, string name, int priority)
    {
        if (string.IsNullOrWhiteSpace(code))
            return ResultDomain<Supplier>.Error(new MessageItem
            { Property = nameof(code), Code = ProcurementResourceConstants.SUPPLIER_CODE_REQUIRED });
        if (string.IsNullOrWhiteSpace(name))
            return ResultDomain<Supplier>.Error(new MessageItem
            { Property = nameof(name), Code = ProcurementResourceConstants.SUPPLIER_NAME_REQUIRED });
        if (priority <= 0)
            return ResultDomain<Supplier>.Error(new MessageItem
            { Property = nameof(priority), Code = ProcurementResourceConstants.SUPPLIER_PRIORITY_INVALID });

        return ResultDomain<Supplier>.Ok(new Supplier { Code = code, Name = name, Priority = priority });
    }

    /// <summary>Kategori eşleme tablosunu topluca değiştirir (seed idempotent günceller).</summary>
    /// <remarks>Handler: ProcurementSeedHostedService</remarks>
    public ResultDomain SetCategoryMappings(IEnumerable<CategoryMapping> mappings)
    {
        _categoryMappings.Clear();
        _categoryMappings.AddRange(mappings);
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>Tedarikçiye özgü ham kategori adını kanonik eşlemeye çözer. Boş/eşlenemeyen ad
    /// Error döner — satır kategorisiz havuza girer, enrich tamamlar.</summary>
    /// <remarks>Handler: PullSupplierFeedCommandHandler</remarks>
    public ResultDomain<CategoryMapping> ResolveCategory(string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return ResultDomain<CategoryMapping>.Error(new MessageItem
            { Property = nameof(rawName), Code = ProcurementResourceConstants.CATEGORY_MAPPING_NOT_FOUND });

        var key = CategoryMapping.Normalize(rawName);
        var mapping = _categoryMappings.FirstOrDefault(m => m.SupplierCategoryName == key);
        if (mapping is null)
            return ResultDomain<CategoryMapping>.Error(new MessageItem
            { Property = nameof(rawName), Code = ProcurementResourceConstants.CATEGORY_MAPPING_NOT_FOUND });

        return ResultDomain<CategoryMapping>.Ok(mapping);
    }
}