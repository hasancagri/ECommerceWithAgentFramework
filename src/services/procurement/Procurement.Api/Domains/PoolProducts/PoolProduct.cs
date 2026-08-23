namespace Procurement.Api.Domains.PoolProducts;

/// <summary>
/// Havuz ürünü — Procurement BC'nin kök aggregate'i. Tutarlılık sınırı BARKOD'dur (R1). 047: barkod
/// global tekil → barkod-başı TEK tedarikçi (buy-box/çoklu-offer söküldü). Tek SupplierListing +
/// birleştirilmiş kanonik içerik + durum tek aggregate'te yaşar. Marten identity Barcode'dur
/// (Identity(x => x.Barcode), string); AggregateRoot.Id (Guid) denetim alanı olarak kalır.
/// Silme yok: feed'den düşen listing Delisted işaretlenir (FR-006) → stok 0, kanonik korunur.
/// Durum makinesi: Pending →(eksiksiz, AI'sız)→ Published; Pending →(enrich)→ Enriched →→ Published.
/// İdempotency tek noktada: TryTakePublish (PublishedContentHash/Price/Stock) — satır-düzeyi diff yok.
/// </summary>
public class PoolProduct : AggregateRoot
{
    public string Barcode { get; private set; } = default!;

    // 047: tek tedarikçi (barkod tekil). null = henüz listing yok.
    public SupplierListing? Listing { get; private set; }

    public CanonicalContent? Canonical { get; private set; }
    public PoolProductStatus Status { get; private set; } = PoolProductStatus.Pending;
    public EnrichmentResult? Enrichment { get; private set; }

    // 047: yayınlanmış içerik + teklif (fiyat/stok) — tek publish-gate'in karşılaştırma temeli.
    public string? PublishedContentHash { get; private set; }
    public decimal? PublishedPrice { get; private set; }
    public int? PublishedStock { get; private set; }

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

    /// <summary>Barkodun güncel satış teklifi: aktif listing'in fiyat/stoğu; listing yok/delisted ise
    /// stok 0 + fiyat son bilinen (PublishedPrice ?? listing fiyatı). Buy-box yok (tek tedarikçi).</summary>
    public CurrentOffer CurrentOffer
    {
        get
        {
            if (Listing is null)
                return CurrentOffer.Create(PublishedPrice ?? 0m, 0);
            if (Listing.IsDelisted)
                return CurrentOffer.Create(PublishedPrice ?? Listing.Price, 0);
            return CurrentOffer.Create(Listing.Price, Listing.Stock);
        }
    }

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

    /// <summary>Tedarikçi satırını upsert eder (047: tek listing, koşulsuz ezme — satır-düzeyi hash-diff
    /// yok). Boş ad ve negatif fiyat/stok reddedilir. İdempotency yayın kararında toplanır.</summary>
    /// <remarks>Handler: PullSupplierFeedCommandHandler</remarks>
    public ResultDomain UpsertListing(Guid supplierId, ListingRow row)
    {
        var messages = new List<MessageItem>();
        if (string.IsNullOrWhiteSpace(row.Name))
            messages.Add(new MessageItem { Property = nameof(row.Name), Code = ProcurementResourceConstants.LISTING_NAME_REQUIRED });
        if (row.Price < 0)
            messages.Add(new MessageItem { Property = nameof(row.Price), Code = ProcurementResourceConstants.LISTING_PRICE_NEGATIVE });
        if (row.Stock < 0)
            messages.Add(new MessageItem { Property = nameof(row.Stock), Code = ProcurementResourceConstants.LISTING_STOCK_NEGATIVE });
        if (messages.Count > 0)
            return ResultDomain.Error(messages);

        if (Listing is null)
            Listing = SupplierListing.Create(supplierId, row);
        else
            Listing.Refresh(row);

        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>Feed'de görünmeyen tedarikçi satırını işaretler (idempotent; silme yok). Delisted listing
    /// stok 0 verir; kanonik korunur (ürün vitrinde kalır).</summary>
    /// <remarks>Handler: PullSupplierFeedCommandHandler</remarks>
    public ResultDomain MarkDelisted(Guid supplierId)
    {
        if (Listing is null || Listing.SupplierId != supplierId || Listing.IsDelisted)
            return ResultDomain.Ok();

        Listing.Delist();
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>Kanonik içeriği tek listing'ten kurar (047: priority-merge YOK — tek tedarikçi). Delisted/
    /// yok ise son kanonik korunur. Hâlâ eksik alanlar saklı enrich sonucundan dolar (overlay). Eksik
    /// kalan içerik Status=Pending yapar (enrich yolu).</summary>
    /// <remarks>Handler: PullSupplierFeedCommandHandler, EnrichPoolProductCommandHandler</remarks>
    public ResultDomain RebuildCanonical()
    {
        if (Listing is null || Listing.IsDelisted)
            return ResultDomain.Ok(); // listing yok/delisted: son kanonik korunur (ürün vitrinde kalır)

        var l = Listing;
        var name = l.Name ?? string.Empty;
        var description = l.Description ?? string.Empty;
        var brand = l.Brand ?? string.Empty;
        var category = l.CanonicalCategory ?? string.Empty;
        var subCategory = l.CanonicalSubCategory ?? string.Empty;
        var sku = l.SupplierSku;
        var dimensions = l.Dimensions;

        // 043: aynı attribute'un tekrarı düşer (tek kaynak; sıra-bağımsız değil, gerek yok).
        var specs = l.CanonicalSpecs
            .GroupBy(s => s.Attribute)
            .Select(g => g.First())
            .ToList();

        // 045: familyCode doğrudan listing'ten.
        var familyCode = l.FamilyCode;

        var merged = CanonicalContent.Create(name, description, brand, category, subCategory, sku, dimensions, specs, familyCode);
        MergedContentHash = merged.ComputeHash();

        // Enrich overlay: yalnız hâlâ eksik içerik alanları saklı AI sonucundan dolar (FR-009).
        if (string.IsNullOrWhiteSpace(description) && !string.IsNullOrWhiteSpace(Enrichment?.Description))
            description = Enrichment!.Description!;
        if (string.IsNullOrWhiteSpace(category) && !string.IsNullOrWhiteSpace(Enrichment?.Category))
        {
            category = Enrichment!.Category!;
            subCategory = Enrichment.SubCategory ?? string.Empty;
        }

        // 043 overlay: listing'in vermediği attribute'lar AI seçiminden dolar (merge daima önce).
        if (Enrichment is not null)
            specs = specs.Concat(Enrichment.Specs
                    .Where(e => specs.All(s => s.Attribute != e.Attribute)))
                .ToList();

        Canonical = CanonicalContent.Create(name, description, brand, category, subCategory, sku, dimensions, specs, familyCode);
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
                category, subCategory, Canonical.Sku, Canonical.Dimensions, specs, Canonical.FamilyCode);
        }

        Status = PoolProductStatus.Enriched;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>Yayın kararı (047: tek kanal). Kanonik complete DEĞİLSE NoChange. İçerik hash'i VEYA
    /// güncel teklif (fiyat/stok) yayınlanmıştan farklıysa PublishCanonical; yayın-sonrası durum
    /// (PublishedContentHash/Price/Stock/Status) güncellenir. Buy-box olayı yok.</summary>
    /// <remarks>Handler: PublishPoolProductCommandHandler</remarks>
    public ResultDomain<PublishDecision> TryTakePublish()
    {
        if (Canonical is null || !Canonical.IsComplete)
            return ResultDomain<PublishDecision>.Ok(PublishDecision.NoChange());

        var offer = CurrentOffer;
        var contentChanged = Canonical.ComputeHash() != PublishedContentHash;
        var offerChanged = offer.Price != PublishedPrice || offer.Stock != PublishedStock;
        if (!contentChanged && !offerChanged)
            return ResultDomain<PublishDecision>.Ok(PublishDecision.NoChange());

        PublishedContentHash = Canonical.ComputeHash();
        PublishedPrice = offer.Price;
        PublishedStock = offer.Stock;
        Status = PoolProductStatus.Published;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain<PublishDecision>.Ok(PublishDecision.Publish());
    }
}

/// <summary>Havuz kaydının yaşam döngüsü durumu (FR-006).</summary>
public enum PoolProductStatus
{
    Pending = 1,
    Enriched = 2,
    Published = 3,
}
