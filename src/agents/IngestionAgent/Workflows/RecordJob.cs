namespace IngestionAgent.Workflows;

// Mesaj başına iş dosyası (kalıcı DEĞİL, FR-017): workflow'dan akar, her executor kendi alanını
// doldurur. Failure dolduğunda sonraki executor'lar kaydı DOKUNMADAN geçirir (short-circuit).
public sealed class RecordJob
{
    public required IntegrationEvents.SupplierProductSnapshotReceived Message { get; init; }

    public Guid? ProductId { get; set; } // CatalogWrite doldurur (upsert cevabından)
    public string? Failure { get; set; } // dolarsa handler exception'a çevirir → retry/DLQ
    public bool Completed { get; set; } // son executor işaretler; iptalle yarım kalan run başarı sayılmaz
}

public static class Failures
{
    public static string Describe(string code, string? detail)
        => string.IsNullOrWhiteSpace(detail) ? code : $"{code}: {detail}";
}