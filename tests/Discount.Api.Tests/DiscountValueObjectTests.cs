namespace Discount.Api.Tests;

public class DiscountRateTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void Create_WithinRange_ReturnsOk(decimal rate)
    {
        var result = DiscountRate.Create(rate);

        result.IsSuccess.ShouldBeTrue();
        result.Data!.Value.ShouldBe(rate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Create_OutOfRange_ReturnsError(decimal rate)
    {
        var result = DiscountRate.Create(rate);

        result.IsSuccess.ShouldBeFalse();
    }
}