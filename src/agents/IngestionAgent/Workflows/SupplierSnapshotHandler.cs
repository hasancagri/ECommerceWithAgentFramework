using IngestionAgent.Workflows._01_CatalogWrite;
using IngestionAgent.Workflows._02_DiscountWrite;

namespace IngestionAgent.Workflows;

// Kanonik mesajın tüketicisi (FR-013): mesaj başına MAF workflow koşar (catalog → discount).
// 012-stock-reservation (Model C): tedarikçi feed'i mevcut ürünün stok adedini EZMEZ. StockWrite
// adımı workflow'dan çıkarıldı; ilk seed zaten ProductCreatedEvent → Stock tüketicisiyle yapılır.
// Başarısız job, IngestionWriteException'a çevrilir → Wolverine retry/DLQ; başarıda sessiz (FR-018).
public sealed class SupplierSnapshotHandler
{
    public static async Task Handle(
        IntegrationEvents.SupplierProductSnapshotReceived message,
        CatalogWriterAgent catalogAgent,
        DiscountWriterAgent discountAgent,
        CancellationToken ct)
    {
        var catalogWrite = new CatalogWriteExecutor(catalogAgent);
        var discountWrite = new DiscountWriteExecutor(discountAgent);

        var workflow = new WorkflowBuilder(catalogWrite)
            .AddEdge(catalogWrite, discountWrite)
            .WithOutputFrom(discountWrite)
            .Build();

        var job = new RecordJob { Message = message };
        await using var run = await InProcessExecution.RunAsync(workflow, job, cancellationToken: ct);

        if (job.Failure is not null)
            throw new IngestionWriteException(message.ExternalId, job.Failure);

        // Dış iptal (Wolverine 60sn execution timeout) RunAsync'ten Failure'sız dönebiliyor;
        // zincir sonuna ulaşmamış run başarı DEĞİLDİR — sessiz ack yerine retry/DLQ (S4 bulgusu).
        if (!job.Completed)
            throw new IngestionWriteException(message.ExternalId, "WORKFLOW_INCOMPLETE");
    }
}