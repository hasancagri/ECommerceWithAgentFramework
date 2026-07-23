namespace Supplier.Gateway.Domains.Feeds;

// Periyodik tetik (FR-001): ilk çekim açılıştan 1 dk sonra, sonrakiler 30 dk arayla (ikisi de config).
// Elle tetikle aynı kapıdan geçer; önceki çekim sürüyorsa tick sessizce atlanır (FR-009).
public sealed class FeedScheduler(FeedPullService pulls, IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var firstDelay = TimeSpan.FromSeconds(configuration.GetValue("Feeds:FirstPullDelaySeconds", 60));
        var interval = TimeSpan.FromMinutes(configuration.GetValue("Feeds:PullIntervalMinutes", 30));

        await Task.Delay(firstDelay, stoppingToken);
        await pulls.TryStartAsync(stoppingToken);

        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await pulls.TryStartAsync(stoppingToken);
    }
}