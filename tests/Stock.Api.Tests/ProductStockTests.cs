namespace Stock.Api.Tests;

public class ProductStockTests
{
    [Fact]
    public void Create_SetsProductIdAndQuantity()
    {
        var productId = Guid.NewGuid();

        var stock = ProductStock.Create(productId, 10);

        stock.ProductId.ShouldBe(productId);
        stock.Quantity.ShouldBe(10);
    }

    [Fact]
    public void Increase_AddsToQuantity()
    {
        var stock = ProductStock.Create(Guid.NewGuid(), 10);

        stock.Increase(5);

        stock.Quantity.ShouldBe(15);
    }

    [Fact]
    public void Decrease_SubtractsFromQuantity()
    {
        var stock = ProductStock.Create(Guid.NewGuid(), 10);

        stock.Decrease(4);

        stock.Quantity.ShouldBe(6);
    }

    [Fact]
    public void Decrease_BelowZero_GoesNegative_NoGuard()
    {
        // Mevcut davranisi belgeler: Decrease negatif stoga izin verir (guard yok).
        // Ileride guard eklenirse bu test kirmizi bayrak olur.
        var stock = ProductStock.Create(Guid.NewGuid(), 3);

        stock.Decrease(5);

        stock.Quantity.ShouldBe(-2);
    }
}