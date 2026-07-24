namespace ChatAgent.Conversations;

// Sahipsiz (anonim) konuşmaların TTL süpürücüsü (FR-009): saatlik tik; login konuşmasına dokunmaz.
// Basit BackgroundService yeterli — tek instance varsayımı (plan); pano/kalıcı zamanlama ihtiyacı yok.
public sealed class AnonymousConversationCleanup(
    ConversationStore store,
    IConfiguration configuration,
    ILogger<AnonymousConversationCleanup> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var ttl = TimeSpan.FromHours(
            configuration.GetValue("Chat:AnonymousTtlHours", ConversationRules.DefaultAnonymousTtlHours));

        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                var deleted = await store.DeleteExpiredAnonymousAsync(DateTimeOffset.UtcNow - ttl, stoppingToken);
                if (deleted > 0)
                    logger.LogInformation("Anonim TTL süpürüldü: {Count} konuşma", deleted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Anonim TTL süpürmesi başarısız; sonraki tikte yeniden denenecek");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}