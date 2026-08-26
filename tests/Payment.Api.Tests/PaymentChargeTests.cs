namespace Payment.Api.Tests;

// 049: tek-faz checkout ödemesi — Charge (Authorize→Capture→Void iki-fazı söküldü, void/refund YOK).
// İlke VI (Domain-TDD): Charge fabrika guard'ları test-first. PSP hop stub (lokal başarılı durum).
public class PaymentChargeTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CheckoutId = Guid.NewGuid();

    [Fact]
    public void Charge_Valid_ReturnsSuccess_WithStubRef()
    {
        var result = PaymentAggregate.Charge(UserId, 250m, CheckoutId);

        result.IsSuccess.ShouldBeTrue();
        result.Data!.Status.ShouldBe(PaymentStatus.Success);
        result.Data!.CheckoutId.ShouldBe(CheckoutId);
        result.Data!.ChargeRef.ShouldNotBeNullOrWhiteSpace();
        result.Data!.UserId.ShouldBe(UserId);
        result.Data!.Amount.ShouldBe(250m);
    }

    [Fact]
    public void Charge_EmptyUserId_ReturnsError()
    {
        var result = PaymentAggregate.Charge(Guid.Empty, 250m, CheckoutId);

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Charge_EmptyCheckoutId_ReturnsError()
    {
        var result = PaymentAggregate.Charge(UserId, 250m, Guid.Empty);

        result.IsSuccess.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Charge_NonPositiveAmount_ReturnsError(decimal amount)
    {
        var result = PaymentAggregate.Charge(UserId, amount, CheckoutId);

        result.IsSuccess.ShouldBeFalse();
    }
}
