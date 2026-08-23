namespace Procurement.Api.Domains.PoolProducts;

/// <summary>
/// Havuz ürünü — Procurement BC'nin kök aggregate'i. Tutarlılık sınırı BARKOD'dur (R1). 047: barkod
/// global tekil → barkod-başı TEK tedarikçi (buy-box/çoklu-offer söküldü). Tek SupplierListing +
/// birleştirilmiş kanonik içerik + durum tek aggregate'te yaşar. Marten identity Barcode'dur
/// (Identity(x => x.Barcode), string); AggregateRoot.Id (Guid) denetim alanı olarak kalır.
/// Silme yok: feed'den düşen listing Delisted işaretlenir (FR-006) → stok 0, kanonik korunur.
/// Durum makinesi: Pending →(kanonik eksiksiz)→ Published (ürünler feed'den eksiksiz gelir — AI enrich yok).
/// İdempotency tek noktada: TryTakePublish (PublishedContentHash/Price/Stock) — satır-düzeyi diff yok.
/// </summary>
public class PoolProduct : AggregateRoot
{
    public string Barcode { get; private set; } = default!;

    // 047: tek tedarikçi (barkod tekil). null = henüz listing yok.
    public SupplierListing? Listing { get; private set; }

    public CanonicalContent? Canonical { get; private set; }
    public PoolProductStatus Status { get; private set; } = PoolProductStatus.Pending;

    // 047: yayınlanmış içerik + teklif (fiyat/stok) — tek publish-gate'in karşılaştırma temeli.
    public string? PublishedContentHash { get; private set; }
    public decimal? PublishedPrice { get; private set; }
    public int? PublishedStock { get; private set; }

    private PoolProduct()
    {
    }

    // ─────────────────────────────────────────────────────────────────────────
    // İŞLEM SIRASI: aşağıdaki metotlar bir barkodun yaşam döngüsünde bu sırayla çalışır.
    //   1. Create           — havuza yeni barkod açılır
    //   2. UpsertListing     — tedarikçi satırı işlenir  (VEYA MarkDelisted: feed'den düştüyse)
    //   3. RebuildCanonical  — kanonik içerik kurulur
    //   4. TryTakePublish    — kanonik eksiksiz + değişmişse yayınlanır
    // (CurrentOffer getter'ı sınıfın SONUNDA — adım değil, türetilmiş "güncel teklif".)
    // ─────────────────────────────────────────────────────────────────────────

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
    /// yok ise son kanonik korunur. Eksik kalan içerik Status=Pending yapar (yayınlanmaz).</summary>
    /// <remarks>Handler: PullSupplierFeedCommandHandler</remarks>
    public ResultDomain RebuildCanonical()
    {
        if (Listing is null || Listing.IsDelisted)
            return ResultDomain.Ok(); // listing yok/delisted: son kanonik korunur (ürün vitrinde kalır)

        var l = Listing;

        // 043: aynı attribute'un tekrarı düşer (tek kaynak; sıra-bağımsız değil, gerek yok).
        var specs = l.CanonicalSpecs
            .GroupBy(s => s.Attribute)
            .Select(g => g.First())
            .ToList();

        Canonical = CanonicalContent.Create(l.Name, l.Description ?? string.Empty, l.Brand,
            l.CanonicalCategory ?? string.Empty, l.CanonicalSubCategory ?? string.Empty,
            l.SupplierSku, l.Dimensions, specs, l.FamilyCode);
        if (!Canonical.IsComplete)
            Status = PoolProductStatus.Pending;

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
}

/// <summary>Havuz kaydının yaşam döngüsü durumu (FR-006).</summary>
public enum PoolProductStatus
{
    Pending = 1,
    Published = 2,
}
