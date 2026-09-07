namespace Storefront.Api.Options;

// 067: semantik arama ayarları. MaxCosineDistance = alaka eşiği — kNN eşiksiz hep Top-N döndürür,
// eşik "alakasız sonucu benzer gibi sunma" (SC-005) garantisidir.
// 0.68 = gerçek veriyle kalibre (2026-09-07): TR sorgu ↔ EN açıklama isabetleri 0.56-0.64 bandında,
// alan-dışı eşleşmeler ≥0.70 — 0.55 TR sorguları yanlışlıkla eliyordu (cross-lingual mesafe payı).
public class SemanticSearchOption
{
    [Range(0.01, 2.0)] public double MaxCosineDistance { get; set; } = 0.68;

    // Backfill'de tek OpenAI isteğine giden metin sayısı (20k katalog ≈ 40 istek → dakikalar, SC-004).
    [Range(1, 2000)] public int BackfillBatchSize { get; set; } = 500;
}