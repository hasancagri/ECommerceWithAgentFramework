namespace FileApi.Options;

// Bir-kez kapak migration'ı yapılandırması. Enabled=false → hosted service erken döner.
// SourceXlsxPath [Required] DEĞİL: kapalıyken boş olabilir; açıkken varlık hosted service'te doğrulanır.
public class CoverMigrationOptions
{
    public const string SectionName = "CoverMigration";

    public bool Enabled { get; set; }

    public string SourceXlsxPath { get; set; } = string.Empty;

    public int DownloadTimeoutSeconds { get; set; } = 30;

    // Eşzamanlı indirme sayısı. Düşük tut (openlibrary + macOS DNS resolver'ı kızdırma) — yavaş ama temiz.
    public int DownloadConcurrency { get; set; } = 2;

    // Her istek denemesi (429/5xx/transient hata → backoff'la yeniden). 404 kalıcı → retry yok.
    public int RetryCount { get; set; } = 4;

    // Her başarılı/denenmiş istekten sonra worker beklemesi (kaynağı boğmama; throttle).
    public int MinDelayMs { get; set; } = 500;

    // Açıklayıcı User-Agent — openlibrary UA'sız/anonim istekleri kısar/bloklar.
    public string UserAgent { get; set; } =
        "ECommerceBookstore-CoverMigration/1.0 (+hasancagridemiriz@gmail.com)";
}
