using Procurement.Api.Domains.PoolProducts.Features.Commands;

namespace Procurement.Api.Infrastructure.Feeds;

// Çekim kapısı: Hangfire cron + manuel uç aynı SemaphoreSlim'den geçer (tek-uçuş; Gateway 007/008
// emsali). Tedarikçi listesi seed'den (DB) okunur; her tedarikçi kendi Wolverine command'iyle işlenir.
public sealed class FeedPullJob(
    IServiceScopeFactory scopeFactory,
    ILogger<FeedPullJob> logger)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    // Manuel uç yolu: kilit alınamazsa false → 409; çekim arka planda koşar (202 dönebilmek için).
    public async Task<bool> TryStartAsync(CancellationToken ct)
    {
        if (!await Gate.WaitAsync(TimeSpan.Zero, ct))
            return false;

        _ = Task.Run(async () =>
        {
            try
            {
                await PullAllAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Feed çekimi beklenmedik şekilde çöktü");
            }
            finally
            {
                Gate.Release();
            }
        }, CancellationToken.None);

        return true;
    }

    // Hangfire yolu: çekimi bekler (süre/başarı job sonucuna yansır); kilit doluysa atlar.
    public async Task RunAsync(CancellationToken ct)
    {
        if (!await Gate.WaitAsync(TimeSpan.Zero, ct))
        {
            logger.LogInformation("Çekim zaten sürüyor; bu koşu atlandı (skipped)");
            return;
        }

        try
        {
            await PullAllAsync(ct);
        }
        finally
        {
            Gate.Release();
        }
    }

    private async Task PullAllAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<IQuerySession>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var codes = await session.Query<Domains.Suppliers.Supplier>()
            .OrderBy(s => s.Priority)
            .Select(s => s.Code)
            .ToListAsync(ct);

        foreach (var code in codes)
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<PullSupplierFeed.PullSupplierFeedResponse>>(
                new PullSupplierFeed.PullSupplierFeedCommand(code), ct);
            if (!result.IsSuccess)
                logger.LogWarning("Pull başarısız: {SupplierCode} ({Codes})", code,
                    string.Join(",", result.Messages.Select(m => m.Code)));
        }
    }
}