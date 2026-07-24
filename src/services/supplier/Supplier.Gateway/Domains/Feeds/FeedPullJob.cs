namespace Supplier.Gateway.Domains.Feeds;

// İnce Hangfire sarmalayıcısı: iş mantığı FeedPullService'tedir; ct'yi koşum anında Hangfire enjekte eder.
public sealed class FeedPullJob(FeedPullService pulls)
{
    public Task RunAsync(CancellationToken ct) => pulls.RunAsync(ct);
}