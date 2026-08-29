using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using WebApp.Options;
using WebApp.Services.Refit;

namespace WebApp.Services.Behavior;

// 048: davranış sinyallerini arka planda Personalization.Api'ye HTTP POST eder (042 JSONL dosyası
// yerine). Sayfa akışı Enqueue ile hiç bloklanmaz; kuyruk dolarsa satır sessizce düşer
// (DropWrite). Arka plan işçi kuyruktan BATCH toplar; POST hata verirse satırlar atlanır
// (kayıp-toleranslı telemetri — Personalization down olsa alışveriş etkilenmez). Yetki:
// personalization.ingest m2m token (PersonalizationSignalsTokenHandler).
public class BehaviorLogWriter(
    IServiceScopeFactory scopeFactory,
    BehaviorLogOptions options,
    ILogger<BehaviorLogWriter> logger)
    : BackgroundService
{
    private const int MaxBatch = 100;

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
            logger.LogInformation("Davranış sinyali gönderimi devre dışı (BehaviorLog:Enabled=false).");
            return;
        }

        while (await _channel.Reader.WaitToReadAsync(stoppingToken))
        {
            // Kuyruktan mevcut olanları batch'le (tek POST — HTTP overhead düşür).
            var batch = new List<BehaviorEvent>(MaxBatch);
            while (batch.Count < MaxBatch && _channel.Reader.TryRead(out var behaviorEvent))
                batch.Add(behaviorEvent);

            if (batch.Count == 0)
                continue;

            try
            {
                using var scope = scopeFactory.CreateScope();
                var client = scope.ServiceProvider.GetRequiredService<IPersonalizationRefitService>();
                var response = await client.PostSignals(batch);
                if (!response.IsSuccessStatusCode)
                    logger.LogWarning("Davranış sinyali batch'i reddedildi ({Count} satır, {Status}) — atlandı.",
                        batch.Count, response.StatusCode);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break; // gerçek host shutdown — döngüyü bırak.
            }
            catch (Exception ex)
            {
                // Kayıp-toleranslı: Personalization erişilemez (HttpClient 5s timeout = TaskCanceledException,
                // shutdown DEĞİL) → batch atlanır, sayfa/host etkilenmez.
                logger.LogWarning(ex, "Davranış sinyali gönderilemedi ({Count} satır) — atlandı.", batch.Count);
            }
        }
    }
}