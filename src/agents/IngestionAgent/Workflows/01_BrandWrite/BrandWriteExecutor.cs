namespace IngestionAgent.Workflows._01_BrandWrite;

// Aşama 1 — marka upsert (016 R10): zincirin girişi. LLM agent'ı upsert_brand'ı çağırır, dönen
// BrandId tipli sonuçla akar. Başarı BrandId'siz OLAMAZ (sahte-başarı emniyeti); başarısız sonuç
// conditional edge ile doğrudan terminale gider (sonraki LLM'ler koşmaz, 015/FR-003 deseni).
public sealed class BrandWriteExecutor(BrandWriterAgent brandAgent)
    : Executor<IntegrationEvents.SupplierProductSnapshotReceived, BrandWriterResult>("brand-write")
{
    private const string BrandWriteFailed = "BRAND_WRITE_FAILED";

    public override async ValueTask<BrandWriterResult> HandleAsync(
        IntegrationEvents.SupplierProductSnapshotReceived message, IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        // Marka zorunlu: boş ad LLM'e gitmeden deterministik hatadır (retry/DLQ devralır).
        if (string.IsNullOrWhiteSpace(message.Brand))
            return new BrandWriterResult(false, Failures.Describe(BrandWriteFailed, "BRAND_MISSING"), null);

        try
        {
            var result = await brandAgent.UpsertAsync(message.Brand, cancellationToken);

            if (result is { IsSuccess: true, BrandId: not null })
                return result;

            return new BrandWriterResult(false,
                Failures.Describe(BrandWriteFailed, result.IsSuccess ? "BRAND_ID_MISSING" : result.Error),
                null);
        }
        catch (Exception ex)
        {
            return new BrandWriterResult(false, Failures.Describe(BrandWriteFailed, ex.Message), null);
        }
    }
}