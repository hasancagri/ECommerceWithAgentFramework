namespace IngestionAgent.Domains;

public enum StagingStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3
}

// StagingGate'in yazma kararı: hangi yazıcılar çağrılacak (deterministik, FR-014).
public sealed record WriteDecision(bool AssumedNew, bool WriteStock, bool SetDiscount, bool RemoveDiscount);

// Çekilen kaydın ingestion tarafındaki izi ve run'lar arası hafızası (FR-009). İş süreçleri
// (hash kapısı, yazma kararı, durum geçişleri) bu modelin metotlarındadır; executor'lar
// yalnız orkestre eder. Id = tedarikçi harici kimliği (tek tedarikçi), Normalized = son içerik.
public class StagingRecord
{
    public string Id { get; private set; } = default!;
    public FeedRecord Normalized { get; private set; } = default!;
    public Guid? CatalogProductId { get; private set; }
    public StagingStatus Status { get; private set; }
    public string? ErrorReason { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }

    private StagingRecord() { } // Marten/Newtonsoft

    public static StagingRecord CreateFor(FeedRecord record) => new()
    {
        Id = record.ExternalId,
        Status = StagingStatus.Pending
    };

    // İçerik kapısı (FR-012): başarıyla işlenmiş ve içeriği değişmemiş kayıt yeniden işlenmez.
    // Kıyas record değer eşitliğiyle — ayrıca hash tutulmaz.
    public bool IsUnchanged(FeedRecord incoming)
        => Status == StagingStatus.Completed && Normalized == incoming;

    // Yazma kararı — üç yol: create / update (yalnız değişen alanlar) / failed-retry.
    // Absorb'dan ÖNCE çağrılmalıdır: kıyas, hafızadaki ESKİ Normalized ile yapılır.
    public WriteDecision DecideWrites(FeedRecord incoming)
    {
        if (CatalogProductId is null)
        {
            // Create beklenir: stok, upsert_product'ın initialStock'uyla event üzerinden
            // açılır (R8) → ayrıca set_stock gerekmez. İndirim yüzdesi varsa yazılır.
            return new WriteDecision(
                AssumedNew: true,
                WriteStock: false,
                SetDiscount: incoming.DiscountPercent.HasValue,
                RemoveDiscount: false);
        }

        if (Status == StagingStatus.Completed && Normalized is not null)
        {
            // Update yolu (US3): yüzde doluysa ve değiştiyse yeniden yazılır;
            // doluydu → boş geldi ise kaldırılır (FR-026).
            return new WriteDecision(
                AssumedNew: false,
                WriteStock: Normalized.StockQuantity != incoming.StockQuantity,
                SetDiscount: incoming.DiscountPercent.HasValue
                             && Normalized.DiscountPercent != incoming.DiscountPercent,
                RemoveDiscount: Normalized.DiscountPercent.HasValue
                                && !incoming.DiscountPercent.HasValue);
        }

        // Failed retry (FR-021): önceki deneme yarım kalmış olabilir; diff'e güvenilemez →
        // stok tam senkron, indirim yeni kayda göre yazılır.
        return new WriteDecision(
            AssumedNew: false,
            WriteStock: true,
            SetDiscount: incoming.DiscountPercent.HasValue,
            RemoveDiscount: Normalized?.DiscountPercent is not null && !incoming.DiscountPercent.HasValue);
    }

    // Yeni içeriği hafızaya işler; kalıcılaştırma ve nihai durum çağıranın sorumluluğunda.
    public void Absorb(FeedRecord normalized)
    {
        Normalized = normalized;
        ErrorReason = null;
    }

    public void LinkCatalogProduct(Guid productId) => CatalogProductId = productId;

    public void MarkCompleted()
    {
        Status = StagingStatus.Completed;
        ErrorReason = null;
        ProcessedAtUtc = DateTime.UtcNow;
    }

    public void MarkFailed(string reason)
    {
        Status = StagingStatus.Failed;
        ErrorReason = reason;
        ProcessedAtUtc = DateTime.UtcNow;
    }
}