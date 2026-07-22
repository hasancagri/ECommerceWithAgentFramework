namespace IngestionAgent.Workflows;

// Periyodik tetik: 30 dakikada bir TryStartAsync. Önceki run hâlâ sürüyorsa kilit vermez
// (false) ve o tick sessizce atlanır — üst üste binme olmaz. Elle tetikleme ucu aynen kalır;
// ikisi aynı kapıdan (tek-run kilidi) geçer. İlk koşu açılıştan 30 dk sonra.
public sealed class IngestionScheduler(IngestionRunService runs) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await runs.TryStartAsync(stoppingToken);
    }
}