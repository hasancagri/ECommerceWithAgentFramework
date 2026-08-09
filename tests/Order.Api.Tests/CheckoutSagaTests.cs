
namespace Order.Api.Tests;

// 028: saga karar cekirdegi testleri — On* metotlari saftir (gRPC/host yok).
public class CheckoutSagaTests
{
    private static CommitResult Ok() => new() { Success = true, Code = string.Empty };
    private static CommitResult Fail(string code) => new() { Success = false, Code = code };

    private static CheckoutSaga NewSaga(int itemCount = 2)
    {
        var items = Enumerable.Range(0, itemCount)
            .Select(_ => new CheckoutItem(Guid.NewGuid(), 1))
            .ToList();
        return new CheckoutSaga { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Items = items };
    }

    // --- US1: mutlu yol ---

    [Fact]
    public void OnCommitResult_Success_AdvancesToNextItem()
    {
        var saga = NewSaga(2);

        var step = saga.OnCommitResult(Ok());

        step.Message.ShouldBeOfType<CommitNextItem>();
        step.Delay.ShouldBeNull();
        saga.NextIndex.ShouldBe(1);
        saga.CommittedItems.Count.ShouldBe(1);
        saga.Phase.ShouldBe(CheckoutPhases.CommittingStock);
    }

    [Fact]
    public void OnCommitResult_LastItemSuccess_MovesToClearingBasket()
    {
        var saga = NewSaga(1);

        var step = saga.OnCommitResult(Ok());

        step.Message.ShouldBeOfType<ClearBasketStep>();
        saga.Phase.ShouldBe(CheckoutPhases.ClearingBasket);
        saga.AllItemsCommitted.ShouldBeTrue();
    }

    [Fact]
    public void OnClearBasketResult_Success_CompletesSaga()
    {
        var saga = NewSaga(1);
        saga.OnCommitResult(Ok());

        var step = saga.OnClearBasketResult(success: true);

        step.CompleteSaga.ShouldBeTrue();
        step.CancelWithReason.ShouldBeNull();
    }

    // --- US2: telafi ---

    [Fact]
    public void OnCommitResult_BusinessError_StartsCompensation_NoRetry()
    {
        var saga = NewSaga(2);
        saga.OnCommitResult(Ok()); // 1. kalem commit edildi

        var step = saga.OnCommitResult(Fail("STOCK_INSUFFICIENT"));

        var compensate = step.Message.ShouldBeOfType<CompensateCheckout>();
        compensate.ReasonCode.ShouldBe("STOCK_INSUFFICIENT");
        step.Delay.ShouldBeNull(); // is hatasi retry EDILMEZ
        saga.Phase.ShouldBe(CheckoutPhases.Compensating);
        saga.CommittedItems.Count.ShouldBe(1); // telafi listesi korunur
    }

    [Fact]
    public void OnRevertResult_Success_ProcessesNextCommittedItem()
    {
        var saga = NewSaga(3);
        saga.OnCommitResult(Ok());
        saga.OnCommitResult(Ok()); // 2 kalem commit edildi
        saga.OnCommitResult(Fail("STOCK_INSUFFICIENT"));

        var step = saga.OnRevertResult(Ok(), "STOCK_INSUFFICIENT");

        step.Message.ShouldBeOfType<CompensateCheckout>();
        saga.CommittedItems.Count.ShouldBe(1); // biri geri alindi, biri kaldi
    }

    [Fact]
    public void OnRevertResult_AllReverted_CancelsOrderAndCompletes()
    {
        var saga = NewSaga(2);
        saga.OnCommitResult(Ok());
        saga.OnCommitResult(Fail("STOCK_INSUFFICIENT"));
        saga.OnRevertResult(Ok(), "STOCK_INSUFFICIENT"); // tek commit'i geri aldi

        var step = saga.OnRevertResult(null, "STOCK_INSUFFICIENT");

        step.CompleteSaga.ShouldBeTrue();
        step.CancelWithReason.ShouldBe("STOCK_INSUFFICIENT");
        saga.CompensationFailed.ShouldBeFalse();
    }

    [Fact]
    public void OnRevertResult_PermanentFailure_SetsCompensationFailed_ButStillCancels()
    {
        var saga = NewSaga(2);
        saga.OnCommitResult(Ok());
        saga.OnCommitResult(Fail("STOCK_INSUFFICIENT"));

        var step = saga.OnRevertResult(Fail("STOCK_REVERT_WITHOUT_COMMIT"), "STOCK_INSUFFICIENT");

        saga.CompensationFailed.ShouldBeTrue(); // FR-013: alarm + manuel mudahale
        step.CompleteSaga.ShouldBeTrue();
        step.CancelWithReason.ShouldBe("STOCK_INSUFFICIENT");
    }

    // --- US3: teknik retry + watchdog ---

    [Fact]
    public void OnCommitResult_TechnicalError_RetriesWithDelay_UpToMax()
    {
        var saga = NewSaga(1);

        for (var i = 1; i <= CheckoutSaga.MaxAttempts; i++)
        {
            var retry = saga.OnCommitResult(Fail(StockCommitClientProxy.CommitUnavailable));
            retry.Message.ShouldBeOfType<CommitNextItem>();
            retry.Delay.ShouldBe(CheckoutSaga.RetryDelay);
            saga.Attempt.ShouldBe(i);
        }

        // Retry tukendi -> telafi dali.
        var final = saga.OnCommitResult(Fail(StockCommitClientProxy.CommitUnavailable));
        final.Message.ShouldBeOfType<CompensateCheckout>();
        saga.Phase.ShouldBe(CheckoutPhases.Compensating);
    }

    [Fact]
    public void OnTimeout_WhileCommitting_StartsCompensationWithTimeoutReason()
    {
        var saga = NewSaga(2);
        saga.OnCommitResult(Ok());

        var step = saga.OnTimeout();

        var compensate = step.Message.ShouldBeOfType<CompensateCheckout>();
        compensate.ReasonCode.ShouldBe("ORDER_TIMEOUT");
        saga.Phase.ShouldBe(CheckoutPhases.Compensating);
    }

    [Fact]
    public void OnTimeout_WhileCompensating_IsNoOp()
    {
        var saga = NewSaga(1);
        saga.OnCommitResult(Fail("STOCK_INSUFFICIENT")); // telafiye gecti

        var step = saga.OnTimeout();

        step.Message.ShouldBeNull();
        step.CompleteSaga.ShouldBeFalse();
    }

    [Fact]
    public void OnTimeout_AfterPivot_CompletesWithoutCancel()
    {
        var saga = NewSaga(1);
        saga.OnCommitResult(Ok()); // pivot gecildi (ClearingBasket)

        var step = saga.OnTimeout();

        step.CompleteSaga.ShouldBeTrue();
        step.CancelWithReason.ShouldBeNull(); // siparis IPTAL EDILMEZ
    }

    // --- US4: pivot-sonrasi sepet temizligi ---

    [Fact]
    public void OnClearBasketResult_Failure_RetriesThenCompletesWithoutCancel()
    {
        var saga = NewSaga(1);
        saga.OnCommitResult(Ok());

        for (var i = 1; i <= CheckoutSaga.MaxAttempts; i++)
        {
            var retry = saga.OnClearBasketResult(success: false);
            retry.Message.ShouldBeOfType<ClearBasketStep>();
            retry.Delay.ShouldBe(CheckoutSaga.RetryDelay);
        }

        var final = saga.OnClearBasketResult(success: false);

        final.CompleteSaga.ShouldBeTrue();
        final.CancelWithReason.ShouldBeNull(); // FR-009: siparis Confirmed KALIR
    }
}