namespace Order.Api.Options;

// 039: PaymentAttempt reconcile mekanigi config'i — section "CheckoutReconcile". PaymentAttempt
// OnReconcileTick backoff adimlarini + son tarihi buradan tip'li okur (R6). BackoffSeconds tick
// sirasina gore gecikme; liste tukenince son deger tekrar kullanilir. DeadlineSeconds sonrasi terminal.
public class CheckoutReconcile
{
    public int[] BackoffSeconds { get; set; } = [5, 15, 60, 300];
    public int DeadlineSeconds { get; set; } = 3600;
}
