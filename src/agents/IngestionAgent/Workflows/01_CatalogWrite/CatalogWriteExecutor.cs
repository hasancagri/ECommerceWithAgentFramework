namespace IngestionAgent.Workflows._01_CatalogWrite;

// Aşama 1 — katalog upsert (FR-014): create/update kararı Catalog'un deterministik kodunda;
// senkron cevaptaki ProductId + Action job'a yazılır, sonraki yazıcılar buna göre karar verir.
public sealed class CatalogWriteExecutor(CatalogWriterAgent catalogAgent)
    : Executor<RecordJob, RecordJob>("catalog-write")
{
    private const string CatalogWriteFailed = "CATALOG_WRITE_FAILED";

    public override async ValueTask<RecordJob> HandleAsync(
        RecordJob job, IWorkflowContext context, CancellationToken cancellationToken)
    {
        try
        {
            var outcome = await catalogAgent.UpsertProductAsync(job.Message, cancellationToken);

            if (outcome.Success && outcome.ProductId is not null)
            {
                job.ProductId = outcome.ProductId;
                job.CatalogAction = outcome.Action;
            }
            else
            {
                job.Failure = Failures.Describe(CatalogWriteFailed, outcome.Error);
            }
        }
        catch (Exception ex)
        {
            job.Failure = Failures.Describe(CatalogWriteFailed, ex.Message);
        }

        return job;
    }
}