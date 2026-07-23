using IngestionAgent.Workflows._01_CatalogWrite;
using IngestionAgent.Workflows._02_DomainWrite.Agents;
using IngestionAgent.Workflows._02_StockWrite;
using IngestionAgent.Workflows._03_DiscountWrite;

namespace IngestionAgent.Workflows;

// Kanonik mesajın tüketicisi (FR-013): mesaj başına MAF workflow koşar (catalog → stock → discount).
// Başarısız job, IngestionWriteException'a çevrilir → Wolverine retry/DLQ politikası tetiklenir;
// başarıda sonuç KİMSEYE bildirilmez (FR-018, tek yönlü akış).
public sealed class SupplierSnapshotHandler
{
    public static async Task Handle(
        IntegrationEvents.SupplierProductSnapshotReceived message,
        CatalogWriterAgent catalogAgent,
        StockWriterAgent stockAgent,
        DiscountWriterAgent discountAgent,
        CancellationToken ct)
    {
        var catalogWrite = new CatalogWriteExecutor(catalogAgent);
        var stockWrite = new StockWriteExecutor(stockAgent);
        var discountWrite = new DiscountWriteExecutor(discountAgent);

        var workflow = new WorkflowBuilder(catalogWrite)
            .AddEdge(catalogWrite, stockWrite)
            .AddEdge(stockWrite, discountWrite)
            .WithOutputFrom(discountWrite)
            .Build();

        var job = new RecordJob { Message = message };
        await using var run = await InProcessExecution.RunAsync(workflow, job, cancellationToken: ct);

        if (job.Failure is not null)
            throw new IngestionWriteException(message.ExternalId, job.Failure);
    }
}