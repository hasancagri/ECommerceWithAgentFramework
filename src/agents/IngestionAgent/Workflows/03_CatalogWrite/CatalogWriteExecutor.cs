namespace IngestionAgent.Workflows._03_CatalogWrite;

// Aşama 1 — katalog upsert: LLM agent'ı upsert_product'ı çağırır, tipli CatalogWriterResult AKAR
// (015: RecordJob kalktı). Başarı ProductId'siz OLAMAZ (sahte-başarı emniyeti, FR-006/SC-002);
// başarısız sonuç conditional edge ile doğrudan terminale gider (sonraki LLM'ler koşmaz, FR-003).
public sealed class CatalogWriteExecutor(CatalogWriterAgent catalogAgent)
    : Executor<IntegrationEvents.SupplierProductSnapshotReceived, CatalogWriterResult>("catalog-write")
{
    private const string CatalogWriteFailed = "CATALOG_WRITE_FAILED";

    public override async ValueTask<CatalogWriterResult> HandleAsync(
        IntegrationEvents.SupplierProductSnapshotReceived message, IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await catalogAgent.UpsertAsync(message, cancellationToken);

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