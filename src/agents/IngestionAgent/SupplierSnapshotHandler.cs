namespace IngestionAgent;

// Kanonik mesajın tüketicisi: mesaj başına MAF workflow koşar (catalog → stock → discount), üç
// adım da LLM-sürücülüdür (015). Adımlar arasında TİPLİ yazıcı sonuçları akar (RecordJob kalktı);
// short-circuit conditional edge'lerdedir (FR-003): başarısız sonuç doğrudan terminale gider,
// sonraki adımların LLM'i hiç çağrılmaz. Sonuç workflow output'undan okunur; başarısızlık
// IngestionWriteException'a çevrilir → Wolverine retry/DLQ (FR-004), başarıda sessiz ack.
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
        var stockWrite = new StockWriteExecutor(stockAgent, message);
        var discountWrite = new DiscountWriteExecutor(discountAgent, message);
        var finish = new FinishExecutor();

        // Her adımdan ya sonraki adıma (başarı) ya doğrudan terminale (hata) gidilir; semantik
        // spike ile kanıtlı (WorkflowSemanticsSpikeTests, FR-015). ProductId'yi Catalog üretir,
        // sonraki adımlara tipli sonuçların içinde KOD taşır (FR-006, Seçenek A); sıra sabittir.
        var workflow = new WorkflowBuilder(catalogWrite)
            .AddEdge<CatalogWriterResult>(catalogWrite, stockWrite, r => r is { IsSuccess: true })
            .AddEdge<CatalogWriterResult>(catalogWrite, finish, r => r is { IsSuccess: false })
            .AddEdge<StockWriterResult>(stockWrite, discountWrite, r => r is { IsSuccess: true })
            .AddEdge<StockWriterResult>(stockWrite, finish, r => r is { IsSuccess: false })
            .AddEdge(discountWrite, finish)
            .WithOutputFrom(finish)
            .Build();

        await using var run = await InProcessExecution.RunAsync(workflow, message, cancellationToken: ct);

        var outcome = run.NewEvents.OfType<WorkflowOutputEvent>()
            .Select(e => e.As<WriterResult>())
            .LastOrDefault(r => r is not null);

        if (outcome is { IsSuccess: false })
            throw new IngestionWriteException(message.ExternalId, outcome.Error ?? "WRITE_FAILED");

        // Dış iptal (execution timeout) çıktısız dönebilir; terminale ulaşmamış run başarı
        // DEĞİLDİR — sessiz ack yerine retry/DLQ (S4 bulgusu, FR-005).
        if (outcome is null)
            throw new IngestionWriteException(message.ExternalId, "WORKFLOW_INCOMPLETE");
    }
}