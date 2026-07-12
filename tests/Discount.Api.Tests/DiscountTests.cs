namespace Discount.Api.Tests;

public class DiscountTests
{
    [Fact]
    public void Create_EmptyUserId_ReturnsError()
    {
        var result = DiscountAggregate.Create(Guid.Empty, "summer0001", 10m);

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Create_InvalidCode_ReturnsError()
    {
        var result = DiscountAggregate.Create(Guid.NewGuid(), "short", 10m);

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Create_InvalidRate_ReturnsError()
    {
        var result = DiscountAggregate.Create(Guid.NewGuid(), "summer0001", 0m);

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Create_Valid_ReturnsOk_WithUppercasedCodeAndRate()
    {
        var userId = Guid.NewGuid();

        var result = DiscountAggregate.Create(userId, "summer0001", 15m);

        result.IsSuccess.ShouldBeTrue();
        result.Data!.UserId.ShouldBe(userId);
        result.Data!.Code.Value.ShouldBe("SUMMER0001");
        result.Data!.Rate.Value.ShouldBe(15m);
    }
}