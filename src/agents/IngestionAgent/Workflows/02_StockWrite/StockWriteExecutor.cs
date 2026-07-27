namespace IngestionAgent.Workflows._02_StockWrite;

// Aşama 2 — stok (014: feed tek otorite, mutlak set). Girdisi catalog'un tipli sonucu; buraya
// yalnız BAŞARILI sonuç yönlenir (conditional edge). ProductId'yi KOD taşır (Seçenek A): stok
// LLM şeması değişmez, çıkan StockWriterResult'a executor yazar. Adet ctor'daki mesajdan gelir.
public sealed class StockWriteExecutor(
    StockWriterAgent stockAgent, IntegrationEvents.SupplierProductSnapshotReceived message)
    : Executor<CatalogWriterResult, StockWriterResult>("stock-write")
{
    private const string StockWriteFailed = "STOCK_WRITE_FAILED";

    public override async ValueTask<StockWriterResult> HandleAsync(
        CatalogWriterResult catalogResult, IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var productId = catalogResult.ProductId!.Value; // edge yalnız başarıyı getirir; başarı Id'siz olamaz

        try
        {
            var result = await stockAgent.SetAsync(productId, message.StockQuantity, cancellationToken);

            return result.IsSuccess
                ? new StockWriterResult(true, null, productId)
                : new StockWriterResult(false, Failures.Describe(StockWriteFailed, result.Error), productId);
        }
        catch (Exception ex)
        {
            return new StockWriterResult(false, Failures.Describe(StockWriteFailed, ex.Message), productId);
        }
    }
}