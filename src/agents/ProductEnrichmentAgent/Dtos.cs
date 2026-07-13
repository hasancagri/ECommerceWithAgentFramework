namespace ProductEnrichmentAgent;

// Catalog list_incomplete_products tool'undan donen eksik urun; MCP JSON'u FeatureListResultModel<T>
// olarak structured deserialize edilir. Brand, Shared'daki gercek BrandType enum'u (elle map yok).
public sealed record IncompleteProduct(
    Guid Id,
    string Name,
    BrandType Brand,
    bool HasDescription,
    bool HasImage);

// File upload_product_image tool cevabinin (FeatureObjectResultModel<T>) Data govdesi — yalniz Url.
public sealed class UploadResult
{
    public string? Url { get; set; }
}

// Bir alanin (aciklama/gorsel) zenginlestirme sonucu.
public enum FieldResult
{
    Ok,       // uretildi ve yazildi
    Skipped,  // zaten doluydu, dokunulmadi (FR-005/FR-009)
    Failed,   // uretim/yazma basarisiz — alan eksik kaldi (FR-006)
}

// Urun basina zenginlestirme sonucu (DB'ye yazilmaz; toplu kosu raporu icin — FR-007).
public sealed class EnrichmentResult
{
    public Guid ProductId { get; init; }
    public string Name { get; init; } = "";
    public FieldResult Description { get; set; } = FieldResult.Skipped;
    public FieldResult Image { get; set; } = FieldResult.Skipped;
}

// Workflow executor'lari arasinda akan mesaj: orijinal urun + birikimli sonuc. Description executor
// uretir (aciklamayi doldurur), Image executor tuketir (gorseli doldurur) ve EnrichmentResult'i yield eder.
public sealed record EnrichmentState(IncompleteProduct Product, EnrichmentResult Result);

