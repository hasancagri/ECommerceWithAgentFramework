using IngestionAgent.Workflows._01_CatalogWrite;
using IngestionAgent.Workflows._02_DiscountWrite;
using IngestionAgent.Workflows._03_StockWrite;

namespace IngestionAgent.Workflows;

// Kanonik mesajın tüketicisi (FR-013): mesaj başına MAF workflow koşar (catalog → stock → discount).
// 014 (feed = stoğun tek otoritesi): StockWrite adımı geri geldi ve feed OnHand'i mutlak ezer
// (012 Model C "feed stoğu ezmez" duruşu tersine döndü). Stok yalnız bu adımdan yazılır.
// Başarısız job, IngestionWriteException'a çevrilir → Wolverine retry/DLQ; başarıda sessiz (FR-018).
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

        // StockWrite ProductId'ye muhtaç (CatalogWrite doldurur); Discount da öyle → sonda kalır.
        var workflow = new WorkflowBuilder(catalogWrite)
            .AddEdge(catalogWrite, stockWrite)
            .AddEdge(stockWrite, discountWrite)
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