namespace Order.Api.Tests;

public class OrderTests
{
    private static Address SampleAddress() =>
        new("Istanbul", "Kadikoy", "Bagdat", "34000", "No 1");

    private static OrderAggregate NewOrder() =>
        OrderAggregate.Create(Guid.NewGuid(), SampleAddress());

    [Fact]
    public void Create_StartsWaitingForPayment_WithZeroTotalAndTenDigitCode()
    {
        var buyerId = Guid.NewGuid();

        var order = OrderAggregate.Create(buyerId, SampleAddress());

        order.BuyerId.ShouldBe(buyerId);
        order.Status.ShouldBe(OrderStatus.WaitingForPayment);
        order.TotalPrice.ShouldBe(0m);
        order.OrderItems.ShouldBeEmpty();
        order.Code.Length.ShouldBe(10);
        order.Code.ShouldAllBe(c => char.IsDigit(c));
    }

    [Fact]
    public void AddOrderItem_EmptyProductName_ReturnsError()
    {
        var order = NewOrder();

        var result = order.AddOrderItem(Guid.NewGuid(), "", 100m);

        result.IsSuccess.ShouldBeFalse();
        order.OrderItems.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void AddOrderItem_NonPositiveUnitPrice_ReturnsError(decimal unitPrice)
    {
        var order = NewOrder();

        var result = order.AddOrderItem(Guid.NewGuid(), "product", unitPrice);

        result.IsSuccess.ShouldBeFalse();
        order.OrderItems.ShouldBeEmpty();
    }

    [Fact]
    public void AddOrderItem_Valid_AddsItemAndRecalculatesTotal()
    {
        var order = NewOrder();

        var result = order.AddOrderItem(Guid.NewGuid(), "product", 120m);

        result.IsSuccess.ShouldBeTrue();
        order.OrderItems.Count.ShouldBe(1);
        order.TotalPrice.ShouldBe(120m);
    }

    [Fact]
    public void AddOrderItem_Multiple_SumsTotalPrice()
    {
        var order = NewOrder();

        order.AddOrderItem(Guid.NewGuid(), "a", 120m);
        order.AddOrderItem(Guid.NewGuid(), "b", 30m);

        order.OrderItems.Count.ShouldBe(2);
        order.TotalPrice.ShouldBe(150m);
    }

    [Fact]
    public void SetPaidStatus_MarksPaidAndStoresPaymentId()
    {
        var order = NewOrder();
        var paymentId = Guid.NewGuid();

        order.SetPaidStatus(paymentId);

        order.Status.ShouldBe(OrderStatus.Paid);
        order.PaymentId.ShouldBe(paymentId);
    }
}