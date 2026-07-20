namespace Discount.Api.Tests;

public class DiscountTests
{
    [Fact]
    public void Create_EmptyProductId_ReturnsError()
    {
        var result = DiscountAggregate.Create(Guid.Empty, 10m);

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Create_InvalidRate_ReturnsError()
    {
        var result = DiscountAggregate.Create(Guid.NewGuid(), 0m);

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Create_Valid_ReturnsOk_WithProductIdAndRate()
    {
        var productId = Guid.NewGuid();

        var result = DiscountAggregate.Create(productId, 15m);

        result.IsSuccess.ShouldBeTrue();
        result.Data!.ProductId.ShouldBe(productId);
        result.Data!.Rate.Value.ShouldBe(15m);
    }

    [Fact]
    public void UpdateRate_Valid_OverwritesPreviousRate()
    {
        var discount = DiscountAggregate.Create(Guid.NewGuid(), 10m).Data!;

        var result = discount.UpdateRate(20m);

        result.IsSuccess.ShouldBeTrue();
        discount.Rate.Value.ShouldBe(20m);
    }

    [Fact]
    public void UpdateRate_Invalid_ReturnsError_KeepsPreviousRate()
    {
        var discount = DiscountAggregate.Create(Guid.NewGuid(), 10m).Data!;

        var result = discount.UpdateRate(0m);

        result.IsSuccess.ShouldBeFalse();
        discount.Rate.Value.ShouldBe(10m);
    }

    [Fact]
    public void Delete_SetsIsDeleted()
    {
        var discount = DiscountAggregate.Create(Guid.NewGuid(), 10m).Data!;

        discount.Delete();

        discount.IsDeleted.ShouldBeTrue();
    }
}