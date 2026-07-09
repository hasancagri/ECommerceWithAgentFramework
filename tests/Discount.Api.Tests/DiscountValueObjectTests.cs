namespace Discount.Api.Tests;

public class DiscountCodeTests
{
    [Fact]
    public void Create_ValidCode_ReturnsOk_AndUppercases()
    {
        var result = DiscountCode.Create("summer0001");

        result.IsSuccess.ShouldBeTrue();
        result.Data!.Value.ShouldBe("SUMMER0001");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyOrWhitespace_ReturnsError(string code)
    {
        var result = DiscountCode.Create(code);

        result.IsSuccess.ShouldBeFalse();
    }

    [Theory]
    [InlineData("short")]
    [InlineData("elevenchars")]
    public void Create_WrongLength_ReturnsError(string code)
    {
        var result = DiscountCode.Create(code);

        result.IsSuccess.ShouldBeFalse();
    }
}

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