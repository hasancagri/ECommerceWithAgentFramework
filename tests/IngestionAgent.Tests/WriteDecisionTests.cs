using IngestionAgent.Workflows._02_DiscountWrite;

namespace IngestionAgent.Tests;

// State'siz yazım kararları (FR-014/015/016): karar yalnız mesaj + senkron katalog cevabından verilir.
// 012 (Model C): StockWrite adımı workflow'dan kaldırıldı — feed artık stok adedine dokunmaz;
// ilk seed ProductCreatedEvent → Stock tüketicisiyle yapılır. Bu yüzden stok yazım kararı testi yok.
public class WriteDecisionTests
{
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