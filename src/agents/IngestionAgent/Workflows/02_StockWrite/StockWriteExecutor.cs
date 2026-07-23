using IngestionAgent.Workflows._02_DomainWrite.Agents;

namespace IngestionAgent.Workflows._02_StockWrite;

// Aşama 2 — stok: "created" ise atlanır (açılış stoğu ProductCreatedEvent ile açılır, R8/FR-014);
// aksi halde stok mesajdaki MUTLAK miktara eşitlenir (FR-015; karar state'siz, senkron cevaptan).
public sealed class StockWriteExecutor(StockWriterAgent stockAgent)
    : Executor<RecordJob, RecordJob>("stock-write")
{
    private const string StockWriteFailed = "STOCK_WRITE_FAILED";

    public static bool ShouldWrite(string? catalogAction) => catalogAction != CatalogWriterAgent.Created;

    public override async ValueTask<RecordJob> HandleAsync(
        RecordJob job, IWorkflowContext context, CancellationToken cancellationToken)
    {
        if (job.Failure is not null || !ShouldWrite(job.CatalogAction))
            return job;

        try
        {
            var outcome = await stockAgent.SetStockAsync(
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