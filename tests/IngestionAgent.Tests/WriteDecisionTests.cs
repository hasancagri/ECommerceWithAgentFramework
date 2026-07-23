using IngestionAgent.Workflows._02_StockWrite;
using IngestionAgent.Workflows._03_DiscountWrite;

namespace IngestionAgent.Tests;

// State'siz yazım kararları (FR-014/015/016): karar yalnız mesaj + senkron katalog cevabından verilir.
public class WriteDecisionTests
{
    [Fact]
    public void CreatedProduct_SkipsStockWrite()
    {
        // R8: açılış stoğu ProductCreatedEvent ile açılır; set_stock create yolunda çağrılmaz.
        StockWriteExecutor.ShouldWrite("created").ShouldBeFalse();
    }

    [Fact]
    public void UpdatedProduct_WritesStock()
    {
        StockWriteExecutor.ShouldWrite("updated").ShouldBeTrue();
    }

    [Fact]
    public void UnknownCatalogAction_DefaultsToStockWrite()
    {
        // Action beklenmedik/boşsa güvenli taraf tam senkrondur (yazım idempotent, FR-021).
        StockWriteExecutor.ShouldWrite(null).ShouldBeTrue();
        StockWriteExecutor.ShouldWrite("upserted").ShouldBeTrue();
    }

    [Fact]
    public void FilledDiscountPercent_SetsDiscount()
    {
        DiscountWriteExecutor.ShouldSet(15m).ShouldBeTrue();
    }

    [Fact]
    public void EmptyDiscountPercent_RemovesDiscount()
    {
        DiscountWriteExecutor.ShouldSet(null).ShouldBeFalse();
    }
}