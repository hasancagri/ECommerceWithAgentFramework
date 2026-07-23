namespace IngestionAgent.Workflows._01_StagingGate;

// Kayıt başına iş dosyası: workflow'dan satır satır geçer, her executor kendi alanını doldurur.
// Failure/Skipped dolduğunda sonraki executor kaydı DOKUNMADAN geçirir (short-circuit).
public sealed class RecordJob
{
    public required FeedRecord Record { get; init; }

    // StagingGate çıktısı:
    public StagingRecord? Staging { get; set; }
    public bool Skipped { get; set; } // hash kapısı: içerik değişmemiş, yazma yok
    public bool AssumedNew { get; set; } // staging'de CatalogProductId yoktu → create beklenir
    public bool WriteStock { get; set; }
    public bool SetDiscount { get; set; }
    public bool RemoveDiscount { get; set; }

    // DomainWrite çıktısı: tool'dan dönen gerçek aksiyon ("created"/"updated") + hata.
    public string? CatalogAction { get; set; }
    public string? Failure { get; set; }
}