namespace Supplier.Gateway.Domains.Feeds;

public static class HttpClients
{
    public const string Feeds = "feeds";
}

// Çekim hattı: kilit → fetch → mükerrer eleme → kayıt başına kapı → ÖNCE publish SONRA save.
// Save kayıt başına yapılır: çökmede mükerrer penceresi tek kayıttır (R3; kayıp yerine tekrar).
public sealed class FeedPullService(
    IDocumentStore store,
    IHttpClientFactory httpClientFactory,
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<FeedPullService> logger)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    // Zamanlayıcı ve manuel uç aynı kapıdan geçer (FR-009): kilit alınamazsa false → 409.
    // 202 dönebilmek için çekim arka planda koşar (fire-and-forget; sonuç log + kuyruktan izlenir).
    public async Task<bool> TryStartAsync(CancellationToken ct)
    {
        if (!await _gate.WaitAsync(TimeSpan.Zero, ct))
            return false;

        _ = Task.Run(async () =>
        {
            try
            {
                await PullAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Feed çekimi beklenmedik şekilde çöktü");
            }
            finally
            {
                _gate.Release();
            }
        }, CancellationToken.None);

        return true;
    }

    private async Task PullAsync(CancellationToken ct)
    {
        var body = await FetchAsync(ct);
        var records = SupplierFeedAdapter.FirstWins(SupplierFeedAdapter.Parse(body));

        if (records.Count == 0)
        {
            logger.LogInformation("Feed boş ya da erişilemedi; çekim mesajsız kapandı (FR-008)");
            return;
        }

        // IMessageBus scoped → çekim başına scope; Marten session'ı da çekim boyu tek.
        using var scope = scopeFactory.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var published = 0;
        await using var session = store.LightweightSession();

        foreach (var wire in records)
        {
            var incoming = SupplierFeedAdapter.ToCanonical(wire);
            var snapshot = await session.LoadAsync<FeedSnapshot>(incoming.ExternalId, ct)
                           ?? FeedSnapshot.CreateFor(incoming.ExternalId);

            if (snapshot.IsUnchanged(incoming))
                continue;

            // FR-006: önce yayınla, sonra kaydet — çökmede kayıp yerine tekrar tercih edilir.
            await bus.PublishAsync(incoming);
            snapshot.Absorb(incoming);
            session.Store(snapshot);
            await session.SaveChangesAsync(ct);
            published++;
        }

        logger.LogInformation("Feed çekimi bitti: {Total} kayıt, {Published} yayın", records.Count, published);
    }

    // Erişilemeyen feed hata üretmez (FR-008): null döner, sonraki periyotta yeniden denenir.
    private async Task<string?> FetchAsync(CancellationToken ct)
    {
        var supplierBase = configuration["services:supplier-api:http:0"] ?? "http://localhost:5299";

        try
        {
            var client = httpClientFactory.CreateClient(HttpClients.Feeds);
            using var response = await client.GetAsync($"{supplierBase}/v1/feeds", ct);
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception)
        {
            return null;
        }
    }
}