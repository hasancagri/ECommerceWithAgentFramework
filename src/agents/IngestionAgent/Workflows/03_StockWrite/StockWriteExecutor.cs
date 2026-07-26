namespace IngestionAgent.Workflows._03_StockWrite;

// Aşama 2 — stok (014): feed stoğun TEK otoritesidir. set_stock ile OnHand mutlak feed değerine
// ayarlanır (create+update ezer; çift-teslim idempotent). Ara executor short-circuit'lidir:
// job.Failure doluysa dokunmadan geçirir; Completed'ı son executor (Discount) işaretler.
// StockWrite yalnız CatalogWrite ProductId doldurduysa çalışır; hata mevcut ingestion modeliyle
// (job.Failure → IngestionWriteException → retry/DLQ) ele alınır.
public sealed class StockWriteExecutor(StockWriterAgent stockAgent)
    : Executor<RecordJob, RecordJob>("stock-write")
{
    private const string StockWriteFailed = "STOCK_WRITE_FAILED";

    public override async ValueTask<RecordJob> HandleAsync(
        RecordJob job, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        if (job.Failure is not null)
            return job; // short-circuit: Completed'ı zincir sonundaki executor işaretler

        try
        {
            var outcome = await stockAgent.SetAsync(
                job.ProductId!.Value, job.Message.StockQuantity, cancellationToken);

            if (!outcome.Success)
                job.Failure = Failures.Describe(StockWriteFailed, outcome.Error);
        }
        catch (Exception ex)
        {
            job.Failure = Failures.Describe(StockWriteFailed, ex.Message);
        }

        return job;
    }
}