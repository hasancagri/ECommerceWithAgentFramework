using System.Threading.Channels;
using WebApp.Options;

namespace WebApp.Services.Behavior;

// 042: davranış satırlarını arka planda JSONL dosyasına yazar (R1). Sayfa akışı Enqueue ile
// hiç bloklanmaz; kuyruk dolarsa veya yazma patlarsa satır sessizce düşer (kayıp-toleranslı
// telemetri, FR-008) — yalnız ILogger'a warning düşülür. Dosya: behavior-YYYYMMDD.jsonl (UTC).
public class BehaviorLogWriter(BehaviorLogOptions options, ILogger<BehaviorLogWriter> logger)
    : BackgroundService
{
    private readonly Channel<BehaviorEvent> _channel = Channel.CreateBounded<BehaviorEvent>(
        new BoundedChannelOptions(10_000) { FullMode = BoundedChannelFullMode.DropWrite });

    public void Enqueue(BehaviorEvent behaviorEvent)
    {
        if (!options.IsActive) return;
        _channel.Writer.TryWrite(behaviorEvent);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.IsActive)
        {
            logger.LogInformation("Davranış logu devre dışı (BehaviorLog:Directory boş veya Enabled=false).");
            return;
        }

        await foreach (var behaviorEvent in _channel.Reader.ReadAllAsync(stoppingToken))
            try
            {
                Directory.CreateDirectory(options.Directory);
                var path = Path.Combine(options.Directory, $"behavior-{DateTime.UtcNow:yyyyMMdd}.jsonl");
                await File.AppendAllTextAsync(path, behaviorEvent.ToJsonLine() + "\n", stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Davranış log satırı yazılamadı — satır atlandı.");
            }
    }
}