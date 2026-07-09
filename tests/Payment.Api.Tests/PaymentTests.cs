namespace Payment.Api.Tests;

public class PaymentTests
{
    [Fact]
    public void Create_EmptyUserId_ReturnsError()
    {
        var result = PaymentAggregate.Create(Guid.Empty, 100m);

        result.IsSuccess.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Create_NonPositiveAmount_ReturnsError(decimal amount)
    {
        var result = PaymentAggregate.Create(Guid.NewGuid(), amount);

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Create_Valid_ReturnsOk_WithPendingStatus()
    {
        var userId = Guid.NewGuid();

        var result = PaymentAggregate.Create(userId, 250m);

        result.IsSuccess.ShouldBeTrue();
        result.Data!.UserId.ShouldBe(userId);
        result.Data!.Amount.ShouldBe(250m);
        result.Data!.Status.ShouldBe(PaymentStatus.Pending);
    }

    [Theory]
    [InlineData(PaymentStatus.Success)]
    [InlineData(PaymentStatus.Failed)]
    public void SetStatus_ChangesStatus(PaymentStatus status)
    {
        var payment = PaymentAggregate.Create(Guid.NewGuid(), 250m).Data!;

        payment.SetStatus(status);

        payment.Status.ShouldBe(status);
    }
}