namespace Supplier.Gateway.Domains.Feeds;

// İnce Hangfire sarmalayıcısı: iş mantığı FeedPullService'tedir; ct'yi koşum anında Hangfire enjekte eder.
public sealed class FeedPullJob(FeedPullService pulls)
{
    // US3: geçici feed hatası en fazla 2 kez otomatik yeniden denenir; sonra job failed kalır.
    [AutomaticRetry(Attempts = 2)]
    public Task RunAsync(CancellationToken ct) => pulls.RunAsync(ct);
}