namespace Payment.Api.Tests;

// 075: tek-faz NON-3D çekim — gerçek çekimi handler PG'ye yaptırır; Charge fabrikası yalnız başarılı
// sonucu (PG paymentId'siyle) domain'e yazar. İlke VI (Domain-TDD): Charge guard'ları test-first.
public class PaymentChargeTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CheckoutId = Guid.NewGuid();
    private const string PgPaymentId = "pg-pay-123";

    [Fact]
    public void Charge_Valid_ReturnsSuccess_WithPgPaymentId()
    {
        var result = PaymentAggregate.Charge(UserId, 250m, CheckoutId, PgPaymentId);

        result.IsSuccess.ShouldBeTrue();
        result.Data!.Status.ShouldBe(PaymentStatus.Success);
        result.Data!.CheckoutId.ShouldBe(CheckoutId);
        // 075: ChargeRef artık gerçek PG çekim kimliği (mock "PAY-{id}" değil).
        result.Data!.ChargeRef.ShouldBe(PgPaymentId);
        result.Data!.UserId.ShouldBe(UserId);
        result.Data!.Amount.ShouldBe(250m);
    }

    [Fact]
    public void Charge_EmptyUserId_ReturnsError()
    {
        var result = PaymentAggregate.Charge(Guid.Empty, 250m, CheckoutId, PgPaymentId);

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Charge_EmptyCheckoutId_ReturnsError()
    {
        var result = PaymentAggregate.Charge(UserId, 250m, Guid.Empty, PgPaymentId);

        result.IsSuccess.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Charge_NonPositiveAmount_ReturnsError(decimal amount)
    {
        var result = PaymentAggregate.Charge(UserId, amount, CheckoutId, PgPaymentId);

        result.IsSuccess.ShouldBeFalse();
    }
}
