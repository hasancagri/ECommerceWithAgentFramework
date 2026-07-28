namespace IngestionAgent.Workflows._03_CatalogWrite;

// Aşama 3 — katalog upsert: girdisi kategori adımının tipli sonucu (BrandId+CategoryId); buraya
// yalnız BAŞARILI sonuç yönlenir (conditional edge). Ürün alanları ctor'daki mesajdan gelir;
// kimlikler zincirden akar (016 R10). Başarı ProductId'siz OLAMAZ (sahte-başarı emniyeti,
// FR-006/SC-002); başarısız sonuç doğrudan terminale gider (sonraki LLM'ler koşmaz, FR-003).
public sealed class CatalogWriteExecutor(
    CatalogWriterAgent catalogAgent, IntegrationEvents.SupplierProductSnapshotReceived message)
    : Executor<CategoryWriterResult, CatalogWriterResult>("catalog-write")
{
    private const string CatalogWriteFailed = "CATALOG_WRITE_FAILED";

    public override async ValueTask<CatalogWriterResult> HandleAsync(
        CategoryWriterResult categoryResult, IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        // Edge yalnız başarıyı getirir; başarı Id'siz olamaz (kategori zorunlu — null yolu yok).
        var brandId = categoryResult.BrandId!.Value;
        var categoryId = categoryResult.CategoryId!.Value;

        try
        {
            var result = await catalogAgent.UpsertAsync(message, brandId, categoryId, cancellationToken);

            if (result is { IsSuccess: true, ProductId: not null })
                return result;

            return new CatalogWriterResult(false,
                Failures.Describe(CatalogWriteFailed, result.IsSuccess ? "PRODUCT_ID_MISSING" : result.Error),
                null);
        }
        catch (Exception ex)
        {
            return new CatalogWriterResult(false, Failures.Describe(CatalogWriteFailed, ex.Message), null);
        }
    }
}