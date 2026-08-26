namespace Payment.Api.Tests;

// 049: İki-fazlı ödeme durum makinesi — Authorized → Captured | Voided.
// İlke VI (Domain-TDD): geçiş guard'ları test-first. PSP hop stub (lokal durum değişimi).
public class PaymentTwoPhaseTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CheckoutId = Guid.NewGuid();

    [Fact]
    public void Authorize_Valid_ReturnsAuthorized_WithStubRef()
    {
        var result = PaymentAggregate.Authorize(UserId, 250m, CheckoutId);

        result.IsSuccess.ShouldBeTrue();
        result.Data!.State.ShouldBe(PaymentState.Authorized);
        result.Data!.CheckoutId.ShouldBe(CheckoutId);
        result.Data!.AuthorizationRef.ShouldNotBeNullOrWhiteSpace();
        result.Data!.UserId.ShouldBe(UserId);
        result.Data!.Amount.ShouldBe(250m);
    }

    [Fact]
    public void Authorize_EmptyCheckoutId_ReturnsError()
    {
        var result = PaymentAggregate.Authorize(UserId, 250m, Guid.Empty);

        result.IsSuccess.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Authorize_NonPositiveAmount_ReturnsError(decimal amount)
    {
        var result = PaymentAggregate.Authorize(UserId, amount, CheckoutId);

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Capture_FromAuthorized_ReturnsCaptured()
    {
        var payment = PaymentAggregate.Authorize(UserId, 250m, CheckoutId).Data!;

        var result = payment.Capture();

        result.IsSuccess.ShouldBeTrue();
        payment.State.ShouldBe(PaymentState.Captured);
    }

    [Fact]
    public void Void_FromAuthorized_ReturnsVoided()
    {
        var payment = PaymentAggregate.Authorize(UserId, 250m, CheckoutId).Data!;

        var result = payment.Void();

        result.IsSuccess.ShouldBeTrue();
        payment.State.ShouldBe(PaymentState.Voided);
    }

    [Fact]
    public void Void_AfterCaptured_ReturnsError()
    {
        var payment = PaymentAggregate.Authorize(UserId, 250m, CheckoutId).Data!;
        payment.Capture();

        var result = payment.Void();

        result.IsSuccess.ShouldBeFalse();
        payment.State.ShouldBe(PaymentState.Captured);
    }

    [Fact]
    public void Capture_AfterVoided_ReturnsError()
    {
        var payment = PaymentAggregate.Authorize(UserId, 250m, CheckoutId).Data!;
        payment.Void();

        var result = payment.Capture();

        result.IsSuccess.ShouldBeFalse();
        payment.State.ShouldBe(PaymentState.Voided);
    }

    [Fact]
    public void Capture_Twice_IsIdempotentNoOp()
    {
        var payment = PaymentAggregate.Authorize(UserId, 250m, CheckoutId).Data!;
        payment.Capture();

        var result = payment.Capture();

        result.IsSuccess.ShouldBeTrue();
        payment.State.ShouldBe(PaymentState.Captured);
    }

    [Fact]
    public void Void_Twice_IsIdempotentNoOp()
    {
        var payment = PaymentAggregate.Authorize(UserId, 250m, CheckoutId).Data!;
        payment.Void();

        var result = payment.Void();

        result.IsSuccess.ShouldBeTrue();
        payment.State.ShouldBe(PaymentState.Voided);
    }
}