namespace IngestionAgent;

// Kanonik mesajın tüketicisi: mesaj başına MAF workflow koşar; 016 R10 ile zincir 5 yazıcıdır
// (brand → category → catalog → stock → discount), hepsi LLM-sürücülüdür (015). Adımlar arasında
// TİPLİ yazıcı sonuçları akar; kimlikler (BrandId/CategoryId/ProductId) sonuçların içinde KOD ile
// taşınır. Short-circuit conditional edge'lerdedir (FR-003): her adımın hata edge'i sonucu doğrudan
// terminale (finish) taşır, sonraki adımların LLM'i hiç çağrılmaz. finish tek çıkış kapısıdır
// (WithOutputFrom); başarısızlık IngestionWriteException'a çevrilir → Wolverine retry/DLQ (FR-004),
// başarıda sessiz ack.
public sealed class SupplierSnapshotHandler
{
    public static async Task Handle(
        IntegrationEvents.SupplierProductSnapshotReceived message,
        BrandWriterAgent brandAgent,
        CategoryWriterAgent categoryAgent,
        CatalogWriterAgent catalogAgent,
        StockWriterAgent stockAgent,
        DiscountWriterAgent discountAgent,
        CancellationToken ct)
    {
        var brandWrite = new BrandWriteExecutor(brandAgent);
        var categoryWrite = new CategoryWriteExecutor(categoryAgent, message);
        var catalogWrite = new CatalogWriteExecutor(catalogAgent, message);
        var stockWrite = new StockWriteExecutor(stockAgent, message);
        var discountWrite = new DiscountWriteExecutor(discountAgent, message);
        var finish = new FinishExecutor();

        // Her adımdan ya sonraki adıma (başarı) ya doğrudan terminale (hata) gidilir; semantik
        // spike ile kanıtlı (WorkflowSemanticsSpikeTests, FR-015). Sıra sabittir; kategori
        // zorunludur (boş kategori CategoryWrite'ta kesilir, null geçme yolu yoktur).
        var workflow = new WorkflowBuilder(brandWrite)
            .AddEdge<BrandWriterResult>(brandWrite, categoryWrite, r => r is { IsSuccess: true })
            .AddEdge<BrandWriterResult>(brandWrite, finish, r => r is { IsSuccess: false })
            .AddEdge<CategoryWriterResult>(categoryWrite, catalogWrite, r => r is { IsSuccess: true })
            .AddEdge<CategoryWriterResult>(categoryWrite, finish, r => r is { IsSuccess: false })
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