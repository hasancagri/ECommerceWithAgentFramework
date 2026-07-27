using IngestionAgent.Workflows;
using Microsoft.Agents.AI.Workflows;
using Shared;

namespace IngestionAgent.Tests;

// SPIKE (FR-015, S4 emsali): MAF semantiği LLM'siz sahte executor'larla kanıtlanır. Tipli zincir
// (015 refactor: RecordJob yok) için doğrulananlar:
// - Conditional edge başarısız sonucu terminale yönlendirir; sonraki executor HİÇ tetiklenmez.
// - Türetilmiş sonuç (CatalogWriterResult...) taban tipli terminale (Executor<WriterResult,...>)
//   dispatch edilir; WithOutputFrom çıktısı WorkflowOutputEvent'ten tipli okunur.
// - Koşullu edge'li run askıda kalmadan tamamlanır (RunAsync idle'a kadar bekler).
public class WorkflowSemanticsSpikeTests
{
    private static readonly Guid KnownProductId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");

    private static IntegrationEvents.SupplierProductSnapshotReceived NewMessage() =>
        new("sup-1", "sku-1", "Ürün", "Açıklama", "Apple", 10m, 5, null);

    private sealed class FakeCatalog(bool fail, List<string> visits)
        : Executor<IntegrationEvents.SupplierProductSnapshotReceived, CatalogWriterResult>("catalog")
    {
        public override ValueTask<CatalogWriterResult> HandleAsync(
            IntegrationEvents.SupplierProductSnapshotReceived message, IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            visits.Add(Id);
            return ValueTask.FromResult(fail
                ? new CatalogWriterResult(false, "catalog_FAILED", null)
                : new CatalogWriterResult(true, null, KnownProductId));
        }
    }

    private sealed class FakeStock(bool fail, List<string> visits)
        : Executor<CatalogWriterResult, StockWriterResult>("stock")
    {
        public override ValueTask<StockWriterResult> HandleAsync(
            CatalogWriterResult catalogResult, IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            visits.Add(Id);
            return ValueTask.FromResult(fail
                ? new StockWriterResult(false, "stock_FAILED", catalogResult.ProductId)
                : new StockWriterResult(true, null, catalogResult.ProductId));
        }
    }

    private sealed class FakeDiscount(bool fail, List<string> visits)
        : Executor<StockWriterResult, DiscountWriterResult>("discount")
    {
        public override ValueTask<DiscountWriterResult> HandleAsync(
            StockWriterResult stockResult, IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            visits.Add(Id);
            return ValueTask.FromResult(fail
                ? new DiscountWriterResult(false, "discount_FAILED", stockResult.ProductId)
                : new DiscountWriterResult(true, null, stockResult.ProductId));
        }
    }

    private static async Task<(WriterResult? Outcome, List<string> Visits)> RunAsync(
        bool catalogFails = false, bool stockFails = false, bool discountFails = false)
    {
        var visits = new List<string>();
        var catalog = new FakeCatalog(catalogFails, visits);
        var stock = new FakeStock(stockFails, visits);
        var discount = new FakeDiscount(discountFails, visits);
        var finish = new FinishExecutor(); // ÜRETİMDEKİ terminal — taban-tip dispatch'i asıl kanıt

        // Üretimdeki şekil (SupplierSnapshotHandler ile birebir aynı edge kurgusu).
        var workflow = new WorkflowBuilder(catalog)
            .AddEdge<CatalogWriterResult>(catalog, stock, r => r is { IsSuccess: true })
            .AddEdge<CatalogWriterResult>(catalog, finish, r => r is { IsSuccess: false })
            .AddEdge<StockWriterResult>(stock, discount, r => r is { IsSuccess: true })
            .AddEdge<StockWriterResult>(stock, finish, r => r is { IsSuccess: false })
            .AddEdge(discount, finish)
            .WithOutputFrom(finish)
            .Build();

        // Askıda kalan run testi sonsuza dek bekletmesin: iptal → OperationCanceledException → FAIL.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var run = await InProcessExecution.RunAsync(workflow, NewMessage(), cancellationToken: cts.Token);

        var outcome = run.NewEvents.OfType<WorkflowOutputEvent>()
            .Select(e => e.As<WriterResult>())
            .LastOrDefault(r => r is not null);

        return (outcome, visits);
    }

    [Fact]
    public async Task SuccessPath_VisitsAllStepsInOrder_OutputIsSuccessfulDiscountResult()
    {
        var (outcome, visits) = await RunAsync();

        visits.ShouldBe(["catalog", "stock", "discount"]);
        outcome.ShouldBeOfType<DiscountWriterResult>();
        outcome.IsSuccess.ShouldBeTrue();
        ((DiscountWriterResult)outcome!).ProductId.ShouldBe(KnownProductId);
    }

    [Fact]
    public async Task CatalogFailure_SkipsLaterSteps_OutputIsFailedCatalogResult()
    {
        var (outcome, visits) = await RunAsync(catalogFails: true);

        visits.ShouldBe(["catalog"]); // stock/discount HİÇ tetiklenmedi (FR-003)
        outcome.ShouldBeOfType<CatalogWriterResult>();
        outcome!.IsSuccess.ShouldBeFalse();
        outcome.Error.ShouldBe("catalog_FAILED");
    }

    [Fact]
    public async Task StockFailure_SkipsDiscount_OutputIsFailedStockResult()
    {
        var (outcome, visits) = await RunAsync(stockFails: true);

        visits.ShouldBe(["catalog", "stock"]);
        outcome.ShouldBeOfType<StockWriterResult>();
        outcome!.IsSuccess.ShouldBeFalse();
        outcome.Error.ShouldBe("stock_FAILED");
    }

    [Fact]
    public async Task DiscountFailure_StillProducesOutput_WithFailureRecorded()
    {
        var (outcome, visits) = await RunAsync(discountFails: true);

        visits.ShouldBe(["catalog", "stock", "discount"]);
        outcome.ShouldBeOfType<DiscountWriterResult>();
        outcome!.IsSuccess.ShouldBeFalse();
        outcome.Error.ShouldBe("discount_FAILED");
    }
}