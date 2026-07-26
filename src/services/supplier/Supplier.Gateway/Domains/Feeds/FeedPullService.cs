namespace Supplier.Gateway.Domains.Feeds;

public static class HttpClients
{
    public const string Feeds = "feeds";
}

// Çekim hattı: kilit → fetch → mükerrer eleme → kayıt başına ATOMİK commit (013: outbox).
// Kayıt başına giden event + snapshot AYNI Postgres tx'inde commit edilir (ya ikisi ya hiçbiri);
// Wolverine DurabilityAgent commit sonrası mesajı RabbitMQ'ya güvenilir iletir (at-least-once).
public sealed class FeedPullService(
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

    // Hangfire yolu (008): çekimi bekler — süre/başarı/hata job sonucuna yansır. Kilit doluysa
    // çekim yapmadan "skipped" loglar ve normal biter; exception yutulmaz (retry job üstündedir).
    public async Task RunAsync(CancellationToken ct)
    {
        if (!await _gate.WaitAsync(TimeSpan.Zero, ct))
        {
            logger.LogInformation("Çekim zaten sürüyor; bu koşu atlandı (skipped)");
            return;
        }

        try
        {
            await PullAsync(ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task PullAsync(CancellationToken ct)
    {
        var body = await FetchAsync(ct);
        var records = SupplierFeedAdapter.FirstWins(SupplierFeedAdapter.Parse(body));

        if (records.Count == 0)
        {
            logger.LogInformation("Feed boş; çekim mesajsız kapandı");
            return;
        }

        var published = 0;

        // Kayıt = tek iş birimi. Outbox iş-birimi başına kullanılır (Wolverine doc önerisi): her
        // kayıt için taze scope → kendi session + outbox; save'ler arası mesaj birikmesi imkânsız.
        foreach (var wire in records)
        {
            var incoming = SupplierFeedAdapter.ToCanonical(wire);

            using var scope = scopeFactory.CreateScope();
            var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();

            var snapshot = await session.LoadAsync<FeedSnapshot>(incoming.ExternalId, ct)
                           ?? FeedSnapshot.CreateFor(incoming.ExternalId);

            if (snapshot.IsUnchanged(incoming))
                continue;

            // Atomik: outbox mesajı + snapshot ilerlemesi tek SaveChanges tx'inde commit edilir.
            // 013: "önce publish sonra save" ikilemi kalktı — kayıp/tekrar penceresi yok.
            var outbox = scope.ServiceProvider.GetRequiredService<IMartenOutbox>();
            await outbox.PublishAsync(incoming);
            snapshot.Absorb(incoming);
            session.Store(snapshot);
            await session.SaveChangesAsync(ct);
            published++;
        }

        logger.LogInformation("Feed çekimi bitti: {Total} kayıt, {Published} yayın", records.Count, published);
    }

    // 008: erişilemeyen feed artık exception'dır — Hangfire job'ı failed olur, sınırlı retry devreye
    // girer. Boş feed hata değildir (0 kayıt → mesajsız kapanış). Manuel uçta exception loglanır (kontrat aynı).
    private async Task<string?> FetchAsync(CancellationToken ct)
    {
        var supplierBase = configuration["services:supplier-api:http:0"] ?? "http://localhost:5299";

        var client = httpClientFactory.CreateClient(HttpClients.Feeds);
        using var response = await client.GetAsync($"{supplierBase}/v1/feeds", ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(ct);
    }
}