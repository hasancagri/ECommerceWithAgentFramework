namespace IngestionAgent.Workflows._03_DiscountWrite;

// Aşama 3 — indirim: set/remove seçimi LLM prompt kuralında (DiscountWriterAgent). Girdisi stok
// adımının tipli sonucu; yüzde ctor'daki mesajdan, ProductId akan sonuçtan gelir.
public sealed class DiscountWriteExecutor(
    DiscountWriterAgent discountAgent, IntegrationEvents.SupplierProductSnapshotReceived message)
    : Executor<StockWriterResult, DiscountWriterResult>("discount-write")
{
    private const string DiscountWriteFailed = "DISCOUNT_WRITE_FAILED";

    public override async ValueTask<DiscountWriterResult> HandleAsync(
        StockWriterResult stockResult, IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var productId = stockResult.ProductId!.Value; // edge yalnız başarıyı getirir

        try
        {
            var result = await discountAgent.ApplyAsync(productId, message.DiscountPercent, cancellationToken);

            return result.IsSuccess
                ? new DiscountWriterResult(true, null, productId)
                : new DiscountWriterResult(false, Failures.Describe(DiscountWriteFailed, result.Error), productId);
        }
        catch (Exception ex)
        {
            return new DiscountWriterResult(false, Failures.Describe(DiscountWriteFailed, ex.Message), productId);
        }
    }
}