namespace Procurement.Api.Domains.PoolProducts;

/// <summary>
/// Havuz ürünü — Procurement BC'nin kök aggregate'i. Tutarlılık sınırı BARKOD'dur (R1): bir barkodun
/// tüm tedarikçi satırları (SupplierListing), birleştirilmiş kanonik içeriği, durumu ve buy-box kararı
/// tek aggregate'te yaşar — merge + buy-box atomik hesaplanır. Marten identity Barcode'dur
/// (Identity(x => x.Barcode), string); AggregateRoot.Id (Guid) denetim alanı olarak kalır.
/// Silme yok: feed'den düşen listing Delisted işaretlenir (FR-006).
/// Durum makinesi: Pending →(eksiksiz, AI'sız)→ Published; Pending →(enrich)→ Enriched →→ Published;
/// Published →(listing değişimi)→ Pending/Published.
/// </summary>
public class PoolProduct : AggregateRoot
{
    public string Barcode { get; private set; } = default!;

    private readonly List<SupplierListing> _listings = new();
    public IReadOnlyList<SupplierListing> Listings => _listings;

    public CanonicalContent? Canonical { get; private set; }
    public PoolProductStatus Status { get; private set; } = PoolProductStatus.Pending;
    public EnrichmentResult? Enrichment { get; private set; }
    public BuyBoxDecision? PublishedBuyBox { get; private set; }
    public string? PublishedContentHash { get; private set; }

    // Enrich cache anahtarı: listing-merge'in (enrich overlay ÖNCESİ) hash'i. Aynı eksik-girdi
    // için AI tekrar çağrılmaz (FR-009) — karşılaştırma Enrichment.SourceHash ile yapılır.
    public string? MergedContentHash { get; private set; }

    private PoolProduct()
    {
    }

    // Saf getter'lar (Result'a sarılmaz).
    public bool NeedsEnrichment => Canonical is not null && !Canonical.IsComplete;
    public bool HasFreshEnrichment => Enrichment is not null && Enrichment.SourceHash == MergedContentHash;

    // 043: hiç spec'i olmayan kanonik enrich adayıdır — ama yayını BLOKLAMAZ (FR-005).
    public bool NeedsSpecEnrichment => Canonical is not null && Canonical.Specs.Count == 0;

    // JasperFxIgnore: statik Create fabrikası event-sourcing evolver konvansiyonuyla çakışır;
    // bu bir domain fabrikasıdır, projection değil (source generator'ı devre dışı bırakır).
    /// <summary>Havuza yeni barkod açar. Boş barkod reddedilir (AI barkod ÜRETMEZ — FR-010).</summary>
    /// <remarks>Handler: PullSupplierFeedCommandHandler</remarks>
    [JasperFx.Core.JasperFxIgnore]
    public static ResultDomain<PoolProduct> Create(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return ResultDomain<PoolProduct>.Error(new MessageItem
            { Property = nameof(barcode), Code = ProcurementResourceConstants.BARCODE_REQUIRED });

        return ResultDomain<PoolProduct>.Ok(new PoolProduct { Barcode = barcode });
    }

    /// <summary>Tedarikçi satırını upsert eder (hash-diff): aynı hash Unchanged, yeni satır Added,
    /// değişen/yeniden listelenen satır Updated. Boş ad ve negatif fiyat/stok reddedilir.</summary>
    /// <remarks>Handler: PullSupplierFeedCommandHandler</remarks>
    public ResultDomain<ListingChange> UpsertListing(Guid supplierId, int supplierPriority, ListingRow row)
    {
        if (string.IsNullOrWhiteSpace(row.Name))
            return ResultDomain<ListingChange>.Error(new MessageItem
            { Property = nameof(row.Name), Code = ProcurementResourceConstants.LISTING_NAME_REQUIRED });
        if (row.Price < 0)
            return ResultDomain<ListingChange>.Error(new MessageItem
            { Property = nameof(row.Price), Code = ProcurementResourceConstants.LISTING_PRICE_NEGATIVE });
        if (row.Stock < 0)
            return ResultDomain<ListingChange>.Error(new MessageItem
            { Property = nameof(row.Stock), Code = ProcurementResourceConstants.LISTING_STOCK_NEGATIVE });

        var listing = _listings.FirstOrDefault(l => l.SupplierId == supplierId);
        if (listing is null)
        {
            _listings.Add(SupplierListing.Create(supplierId, supplierPriority, row));
            UpdatedTime = DateTime.UtcNow;
            return ResultDomain<ListingChange>.Ok(ListingChange.Added);
        }

        if (listing.ContentHash == row.ComputeContentHash() && !listing.IsDelisted)
        {
            listing.Touch();
            return ResultDomain<ListingChange>.Ok(ListingChange.Unchanged);
        }

        listing.Refresh(supplierPriority, row);
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain<ListingChange>.Ok(ListingChange.Updated);
    }

    /// <summary>Feed'de görünmeyen tedarikçi satırını yarıştan çıkarır (idempotent; silme yok).</summary>
    /// <remarks>Handler: PullSupplierFeedCommandHandler</remarks>
    public ResultDomain MarkDelisted(Guid supplierId)
    {
        var listing = _listings.FirstOrDefault(l => l.SupplierId == supplierId);
        if (listing is null || listing.IsDelisted)
            return ResultDomain.Ok();

        listing.Delist();
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>Kanonik içeriği yeniden birleştirir (R9): alan bazında Priority sırasına göre ilk DOLU
    /// değer kazanır; Delisted satır birleşmeye girmez; hâlâ eksik alanlar saklı enrich sonucundan dolar.
    /// Sonuç sıra-bağımsızdır. Eksik kalan içerik Status=Pending yapar (enrich yolu).</summary>
    /// <remarks>Handler: PullSupplierFeedCommandHandler, EnrichPoolProductCommandHandler</remarks>
    public ResultDomain RebuildCanonical()
    {
        var active = _listings.Where(l => !l.IsDelisted).OrderBy(l => l.SupplierPriority).ToList();
        if (active.Count == 0)
            return ResultDomain.Ok(); // tüm satırlar delisted: son kanonik korunur (ürün vitrinde kalır)

        var name = active.Select(l => l.Name).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
        var description = active.Select(l => l.Description).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
        var brand = active.Select(l => l.Brand).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
        var categorySource = active.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l.CanonicalCategory));
        var category = categorySource?.CanonicalCategory ?? string.Empty;
        var subCategory = categorySource?.CanonicalSubCategory ?? string.Empty;
        var sku = active[0].SupplierSku; // öncelikli tedarikçinin SKU'su
        var dimensions = active.Select(l => l.Dimensions).FirstOrDefault(d => d is not null);

        // 043: attribute-başına merge — Priority sırasında ilk veren kazanır (sıra-bağımsız; R3).
        var specs = active
            .SelectMany(l => l.CanonicalSpecs)
            .GroupBy(s => s.Attribute)
            .Select(g => g.First())
            .ToList();

        var merged = CanonicalContent.Create(name, description, brand, category, subCategory, sku, dimensions, specs);
        MergedContentHash = merged.ComputeHash();

        // Enrich overlay: yalnız hâlâ eksik içerik alanları saklı AI sonucundan dolar (FR-009).
        if (string.IsNullOrWhiteSpace(description) && !string.IsNullOrWhiteSpace(Enrichment?.Description))
            description = Enrichment!.Description!;
        if (string.IsNullOrWhiteSpace(category) && !string.IsNullOrWhiteSpace(Enrichment?.Category))
        {
            category = Enrichment!.Category!;
            subCategory = Enrichment.SubCategory ?? string.Empty;
        }

        // 043 overlay: merge'in vermediği attribute'lar AI seçiminden dolar (merge daima önce).
        if (Enrichment is not null)
            specs = specs.Concat(Enrichment.Specs
                    .Where(e => specs.All(s => s.Attribute != e.Attribute)))
                .ToList();

        Canonical = CanonicalContent.Create(name, description, brand, category, subCategory, sku, dimensions, specs);
        if (!Canonical.IsComplete)
            Status = PoolProductStatus.Pending;

        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>Enrich sonucunu uygular: yalnız eksik İÇERİK alanları dolar (kategori kanonik listeden
    /// olmak zorunda); barkod/ölçü/fiyat/stok'a dokunuş yapısal olarak imkânsızdır (FR-010).
    /// 043: AI spec çiftleri kapalı listeye süzülür — liste-dışı çift DÜŞER, akış durmaz (FR-004).</summary>
    /// <remarks>Handler: EnrichPoolProductCommandHandler</remarks>
    public ResultDomain ApplyEnrichment(EnrichmentResult result,
        IReadOnlyCollection<CanonicalCategoryPair> canonicalCategories,
        IReadOnlyCollection<SpecValue> canonicalSpecs)
    {
        if (!string.IsNullOrWhiteSpace(result.Category) &&
            !canonicalCategories.Any(p => p.Category == result.Category && p.SubCategory == result.SubCategory))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(result.Category), Code = ProcurementResourceConstants.ENRICHMENT_CATEGORY_NOT_CANONICAL });

        // 043 guard: yalnız registry'deki çiftler yaşar; aynı attribute'un tekrarı da düşer.
        var validSpecs = result.Specs
            .Where(s => canonicalSpecs.Contains(s))
            .GroupBy(s => s.Attribute)
            .Select(g => g.First())
            .ToList();
        result = EnrichmentResult.Create(result.SourceHash, result.Description,
            result.Category, result.SubCategory, validSpecs);

        Enrichment = result;

        // Overlay'i kanonik içeriğe hemen yansıt (yalnız eksik alanlar; merge her zaman önceliklidir).
        if (Canonical is not null)
        {
            var description = Canonical.Description;
            var category = Canonical.Category;
            var subCategory = Canonical.SubCategory;

            if (string.IsNullOrWhiteSpace(description) && !string.IsNullOrWhiteSpace(result.Description))
                description = result.Description!;
            if (string.IsNullOrWhiteSpace(category) && !string.IsNullOrWhiteSpace(result.Category))
            {
                category = result.Category!;
                subCategory = result.SubCategory ?? string.Empty;
            }

            // 043: merge'in vermediği attribute'lar AI seçiminden dolar.
            var specs = Canonical.Specs
                .Concat(validSpecs.Where(e => Canonical.Specs.All(s => s.Attribute != e.Attribute)))
                .ToList();

            Canonical = CanonicalContent.Create(Canonical.Name, description, Canonical.Brand,
                category, subCategory, Canonical.Sku, Canonical.Dimensions, specs);
        }

        Status = PoolProductStatus.Enriched;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>Buy-box'ı saf hesaplar (yan etkisiz): stok>0 en ucuz; eşitlikte düşük Priority;
    /// aday yoksa kazanansız karar (Stock 0, fiyat son bilinen).</summary>
    /// <remarks>Handler: PullSupplierFeedCommandHandler, EnrichPoolProductCommandHandler</remarks>
    public ResultDomain<BuyBoxDecision> EvaluateBuyBox()
    {
        var candidates = _listings.Where(l => !l.IsDelisted && l.Stock > 0).ToList();
        if (candidates.Count == 0)
        {
            var lastKnownPrice = PublishedBuyBox?.Price
                ?? _listings.Where(l => !l.IsDelisted)
                    .OrderBy(l => l.Price).ThenBy(l => l.SupplierPriority)
                    .FirstOrDefault()?.Price;
            return ResultDomain<BuyBoxDecision>.Ok(BuyBoxDecision.NoWinner(lastKnownPrice));
        }

        var winner = candidates.OrderBy(l => l.Price).ThenBy(l => l.SupplierPriority).First();
        return ResultDomain<BuyBoxDecision>.Ok(BuyBoxDecision.Winner(winner.SupplierId, winner.Price, winner.Stock));
    }

    /// <summary>Yayın kararını alır: kanonik complete DEĞİLSE NoChange; içerik hash'i değiştiyse
    /// CanonicalProductUpserted, buy-box kararı değiştiyse BuyBoxChanged yayını işaretlenir ve
    /// yayın-sonrası durum (PublishedContentHash/PublishedBuyBox/Status) güncellenir.</summary>
    /// <remarks>Handler: PullSupplierFeedCommandHandler, EnrichPoolProductCommandHandler</remarks>
    public ResultDomain<PublishDecision> TryTakePublish(BuyBoxDecision decision)
    {
        if (Canonical is null || !Canonical.IsComplete)
            return ResultDomain<PublishDecision>.Ok(PublishDecision.NoChange());

        var contentChanged = Canonical.ComputeHash() != PublishedContentHash;
        var buyBoxChanged = !decision.Equals(PublishedBuyBox);
        if (!contentChanged && !buyBoxChanged)
            return ResultDomain<PublishDecision>.Ok(PublishDecision.NoChange());

        PublishedContentHash = Canonical.ComputeHash();
        PublishedBuyBox = decision;
        Status = PoolProductStatus.Published;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain<PublishDecision>.Ok(PublishDecision.Create(contentChanged, buyBoxChanged));
    }
}

/// <summary>Havuz kaydının yaşam döngüsü durumu (FR-006).</summary>
public enum PoolProductStatus
{
    Pending = 1,
    Enriched = 2,
    Published = 3,
}

/// <summary>UpsertListing sonucu (outcome enum — Ok(outcome) ile taşınır).</summary>
public enum ListingChange
{
    Unchanged = 0,
    Added = 1,
    Updated = 2,
}