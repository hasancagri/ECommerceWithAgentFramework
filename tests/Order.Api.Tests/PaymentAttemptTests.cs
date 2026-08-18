using Order.Api.Domains.Orders.Features.Commands;
using Order.Api.Domains.PaymentAttempts;
using Order.Api.Http;
using Order.Api.Options;
using Shouldly;
using Xunit;

namespace Order.Api.Tests;

// 039 (Ilke VI): PaymentAttempt saf karar cekirdegi test-first — OnChargeResult / OnReconcileTick.
// Mock yok; sadece durum + karar gecisleri. US1 (mutlu yol), US2 (verify geidi), US3 (idempotent
// re-entry), US4 (belirsiz -> reconcile -> deadline).
public class PaymentAttemptTests
{
    private static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Merchant = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset Now = new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);

    private static CheckoutReconcile Cfg(int deadline = 3600) =>
        new() { BackoffSeconds = [5, 15, 60], DeadlineSeconds = deadline };

    private static PaymentAttempt NewAttempt(decimal amount = 100m, int deadline = 3600) =>
        PaymentAttempt.Begin(
            "deadbeef", User, Merchant, amount, installment: 1, cardId: null,
            items: [new CreateOrder.OrderItemDto(Guid.NewGuid(), "Urun", amount, 1)],
            address: new CreateOrder.AddressDto("Istanbul", "", "", "", "adres"),
            Now, Cfg(deadline));

    // --- US1: mutlu yol ---

    [Fact]
    public void Charge_success_matching_amount_creates_order()
    {
        var a = NewAttempt(amount: 100m);

        var d = a.OnChargeResult(PaymentOutcome.Success, "pg-1", chargedPrice: 100m, Now, Cfg());

        d.Action.ShouldBe(PaymentAttemptAction.CreateOrder);
        a.Status.ShouldBe(PaymentAttemptStatus.Succeeded);
        a.ProviderPaymentId.ShouldBe("pg-1");
    }

    // --- US2: verify geidi ---

    [Fact]
    public void Charge_success_amount_mismatch_is_rejected_no_order()
    {
        var a = NewAttempt(amount: 100m);

        var d = a.OnChargeResult(PaymentOutcome.Success, "pg-1", chargedPrice: 90m, Now, Cfg());

        d.Action.ShouldBe(PaymentAttemptAction.VerifyFailed);
        a.Status.ShouldBe(PaymentAttemptStatus.Failed);
    }

    [Fact]
    public void Charge_failed_notifies_failure_no_order()
    {
        var a = NewAttempt();

        var d = a.OnChargeResult(PaymentOutcome.Failed, null, 0m, Now, Cfg());

        d.Action.ShouldBe(PaymentAttemptAction.NotifyFailed);
        a.Status.ShouldBe(PaymentAttemptStatus.Failed);
    }

    // --- US3: idempotent re-entry ---

    [Fact]
    public void Already_succeeded_attempt_does_not_recharge()
    {
        var a = NewAttempt(amount: 100m);
        a.OnChargeResult(PaymentOutcome.Success, "pg-1", 100m, Now, Cfg()); // ilk basari

        var d = a.OnChargeResult(PaymentOutcome.Success, "pg-2", 100m, Now, Cfg()); // ikinci giris

        d.Action.ShouldBe(PaymentAttemptAction.AlreadyCompleted);
        a.ProviderPaymentId.ShouldBe("pg-1"); // ilk odeme korunur, yeni cekim yok
    }

    // --- US4: belirsiz -> reconcile ---

    [Fact]
    public void Charge_ambiguous_schedules_reconcile()
    {
        var a = NewAttempt();

        var d = a.OnChargeResult(PaymentOutcome.Ambiguous, null, 0m, Now, Cfg());

        d.Action.ShouldBe(PaymentAttemptAction.ScheduleReconcile);
        d.ReconcileDelay.ShouldBe(TimeSpan.FromSeconds(5)); // ilk backoff
        a.Status.ShouldBe(PaymentAttemptStatus.Unknown);
    }

    [Fact]
    public void Reconcile_tick_success_creates_order()
    {
        var a = NewAttempt(amount: 100m);
        a.OnChargeResult(PaymentOutcome.Ambiguous, null, 0m, Now, Cfg());

        var d = a.OnReconcileTick(PaymentOutcome.Success, "pg-1", 100m, Now.AddSeconds(5), Cfg());

        d.Action.ShouldBe(PaymentAttemptAction.CreateOrder);
        a.Status.ShouldBe(PaymentAttemptStatus.Succeeded);
    }

    [Fact]
    public void Reconcile_tick_failed_marks_failed()
    {
        var a = NewAttempt();
        a.OnChargeResult(PaymentOutcome.Ambiguous, null, 0m, Now, Cfg());

        var d = a.OnReconcileTick(PaymentOutcome.Failed, null, 0m, Now.AddSeconds(5), Cfg());

        d.Action.ShouldBe(PaymentAttemptAction.NotifyFailed);
        a.Status.ShouldBe(PaymentAttemptStatus.Failed);
    }

    [Fact]
    public void Reconcile_tick_pending_before_deadline_reschedules_with_backoff()
    {
        var a = NewAttempt(deadline: 3600);
        a.OnChargeResult(PaymentOutcome.Ambiguous, null, 0m, Now, Cfg(3600));

        var d = a.OnReconcileTick(PaymentOutcome.Ambiguous, null, 0m, Now.AddSeconds(5), Cfg(3600));

        d.Action.ShouldBe(PaymentAttemptAction.ScheduleReconcile);
        a.Status.ShouldBe(PaymentAttemptStatus.Unknown);
        d.ReconcileDelay.ShouldNotBeNull();
    }

    [Fact]
    public void Reconcile_tick_pending_after_deadline_is_terminal()
    {
        var a = NewAttempt(deadline: 0); // DeadlineAt = Now
        a.OnChargeResult(PaymentOutcome.Ambiguous, null, 0m, Now, Cfg(0));

        var d = a.OnReconcileTick(PaymentOutcome.Ambiguous, null, 0m, Now.AddSeconds(1), Cfg(0));

        d.Action.ShouldBe(PaymentAttemptAction.Terminal);
        a.Status.ShouldBe(PaymentAttemptStatus.NeedsReconciliation); // ops gorunurluk, sonsuz degil
    }
}
