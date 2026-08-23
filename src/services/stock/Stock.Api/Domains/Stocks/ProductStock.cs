
namespace Stock.Api.Domains.Stocks;

public class ProductStock : AggregateRoot
{
    public Guid ProductId { get; private set; }

    // 012: kalici alan adi Quantity (OnHand = fiziksel stok). Feed/Commit/restock disinda degismez.
    public int Quantity { get; private set; }

    // Domain OnHand semantigi (I1); ayni deger, okunabilirlik icin.
    [JsonIgnore] public int OnHand => Quantity;

    // 012: aktif rezervasyonlar (gomulu entity). Available bunlardan turetilir.
    [JsonProperty("Reservations")] private List<ReservationEntry> _reservations = new();
    [JsonIgnore] public IReadOnlyList<ReservationEntry> Reservations => _reservations.AsReadOnly();

    // 028: islenmis saga operasyon anahtarlari ("orderId:commit" / "orderId:revert").
    // At-least-once teslimatta mukerrer Commit/RevertCommit'i no-op yapar. Bounded (son 100).
    [JsonProperty("ProcessedOps")] private List<string> _processedOps = new();
    private const int ProcessedOpsLimit = 100;

    private static string CommitKey(Guid orderId) => $"{orderId}:commit";
    private static string RevertKey(Guid orderId) => $"{orderId}:revert";

    private void MarkProcessed(string key)
    {
        _processedOps.Add(key);
        if (_processedOps.Count > ProcessedOpsLimit)
            _processedOps.RemoveRange(0, _processedOps.Count - ProcessedOpsLimit);
    }

    private ProductStock()
    {
    }

    /// <summary>Yeni bir ProductStock aggregate'i verilen urun ve baslangic adediyle olusturur.</summary>
    public static ProductStock Create(Guid productId, int quantity)
    {
        return new ProductStock
        {
            ProductId = productId,
            Quantity = quantity
        };
    }

    /// <summary>Stok adedini verilen miktar kadar artirir (restock).</summary>
    public ResultDomain Increase(int amount)
    {
        Quantity += amount;
        return ResultDomain.Ok();
    }

    /// <summary>Stok adedini verilen miktar kadar azaltir.</summary>
    public ResultDomain Decrease(int amount)
    {
        Quantity -= amount;
        return ResultDomain.Ok();
    }

    // 005-supplier-ingestion: feed mutlak adet verir; set semantigi Increase/Decrease'ten ayridir.
    // Invariant: stok adedi negatif olamaz — kural handler'da degil aggregate'te korunur.
    /// <summary>Feed mutlak adedini set eder; negatif adedi reddeder (invariant).</summary>
    public ResultDomain SetQuantity(int quantity)
    {
        if (quantity < 0)
            return ResultDomain.Error(new MessageItem
            {
                Property = nameof(Quantity),
                Code = StockResourceConstants.STOCK_QUANTITY_CANNOT_BE_NEGATIVE
            });

        Quantity = quantity;
        return ResultDomain.Ok();
    }

    // --- 012-stock-reservation ---

    private int ActiveReservedQuantity(DateTimeOffset now) =>
        _reservations.Where(r => r.IsActiveAt(now)).Sum(r => r.Quantity);

    private int ActiveReservedByOthers(Guid userId, DateTimeOffset now) =>
        _reservations.Where(r => r.UserId != userId && r.IsActiveAt(now)).Sum(r => r.Quantity);

    // Aktif (suresi gecmemis) rezerve edilmis toplam adet (UI "reserved" gosterimi icin).
    /// <summary>Aktif (suresi gecmemis) rezerve edilmis toplam adedi dondurur.</summary>
    public int ReservedAt(DateTimeOffset now) => ActiveReservedQuantity(now);

    // Available = OnHand - aktif rezervasyonlar; oversell'de 0'a kirpilir (G1/FR-017).
    /// <summary>Musait adet = OnHand - aktif rezervasyonlar; 0'a kirpilir.</summary>
    public int AvailableAt(DateTimeOffset now) => Math.Max(0, Quantity - ActiveReservedQuantity(now));

    // Oversell tespiti: tedarikci OnHand'i aktif rezervasyonlarin altina dusurmus olabilir.
    // Log'lama aggregate'te degil handler'da yapilir (aggregate saf kalir).
    /// <summary>OnHand aktif rezervasyonlarin altina dusmus mu (oversell) tespit eder.</summary>
    public bool IsOversoldAt(DateTimeOffset now) => Quantity < ActiveReservedQuantity(now);

    // Sepete ekleme/artirma: kullanicinin bu urun icin rezervasyonunu mutlak adede getirir.
    // Idempotent (ayna model, FR-011). Sabit TTL: ExpiresAt yalniz ilk olusumda atanir (FR-010a).
    // 017: expiresAt (sepet capasi) verilmisse HER durumda uygulanir — yeni rezervasyon onunla dogar,
    // mevcut rezervasyonun bitisi ona esitlenir (cagiran sabit capayi gecirir; rolling-TTL riski yok).
    /// <summary>Kullanicinin bu urun icin rezervasyonunu mutlak adede getirir (idempotent, TTL'li).</summary>
    public ResultDomain SetReservedQuantity(Guid userId, int quantity, TimeSpan ttl, DateTimeOffset now,
        DateTimeOffset? expiresAt = null)
    {
        if (quantity < 0)
            return ResultDomain.Error(new MessageItem
                { Property = nameof(quantity), Code = StockResourceConstants.STOCK_RESERVE_QUANTITY_INVALID });

        var existing = _reservations.FirstOrDefault(r => r.UserId == userId);

        if (quantity == 0)
        {
            if (existing is not null) _reservations.Remove(existing);
            return ResultDomain.Ok();
        }

        // Bu kullanicinin alabilecegi ust sinir = OnHand - digerlerinin aktif rezervasyonu.
        if (quantity > Quantity - ActiveReservedByOthers(userId, now))
            return ResultDomain.Error(new MessageItem
                { Property = nameof(quantity), Code = StockResourceConstants.STOCK_INSUFFICIENT });

        if (existing is null)
        {
            _reservations.Add(new ReservationEntry(userId, quantity, expiresAt ?? now.Add(ttl)));
        }
        else
        {
            existing.SetQuantity(quantity); // ExpiresAt yenilenmez (sabit TTL yolu)
            if (expiresAt is not null)
                existing.SetExpiresAt(expiresAt.Value); // 017: capa esitlenir (idempotent)
        }

        return ResultDomain.Ok();
    }

    // Sepetten cikarma: rezervasyonu tamamen birak (idempotent no-op).
    /// <summary>Kullanicinin rezervasyonunu tamamen birakir (idempotent no-op).</summary>
    public ResultDomain Release(Guid userId)
    {
        var existing = _reservations.FirstOrDefault(r => r.UserId == userId);
        if (existing is not null) _reservations.Remove(existing);
        return ResultDomain.Ok();
    }

    // Siparis: gecerli rezervasyonu kalici stok dususune cevir (OnHand duser, hold kapanir).
    // 028: orderId idempotency anahtari — ayni siparisin mukerrer Commit'i no-op basari doner.
    /// <summary>Gecerli rezervasyonu kalici stok dususune cevirir; orderId ile idempotent.</summary>
    public ResultDomain Commit(Guid userId, int quantity, DateTimeOffset now, Guid orderId = default)
    {
        if (orderId != Guid.Empty && _processedOps.Contains(CommitKey(orderId)))
            return ResultDomain.Ok();

        var existing = _reservations.FirstOrDefault(r => r.UserId == userId && r.IsActiveAt(now));
        if (existing is null || existing.Quantity < quantity)
            return ResultDomain.Error(new MessageItem
                { Code = StockResourceConstants.STOCK_NO_ACTIVE_RESERVATION });

        Quantity -= quantity;

        if (existing.Quantity == quantity)
            _reservations.Remove(existing);
        else
            existing.SetQuantity(existing.Quantity - quantity);

        if (orderId != Guid.Empty) MarkProcessed(CommitKey(orderId));
        return ResultDomain.Ok();
    }

    // 028: saga telafisi — commit edilmis adedi stoga geri ekler. orderId ile idempotent;
    // yalniz daha once commit edilmis siparis geri alinabilir (kacak artis engellenir).
    /// <summary>Saga telafisi: commit edilmis adedi stoga geri ekler; orderId ile idempotent.</summary>
    public ResultDomain RevertCommit(int quantity, Guid orderId)
    {
        if (orderId == Guid.Empty || quantity <= 0)
            return ResultDomain.Error(new MessageItem
                { Code = StockResourceConstants.STOCK_REVERT_INVALID });

        if (_processedOps.Contains(RevertKey(orderId)))
            return ResultDomain.Ok();

        if (!_processedOps.Contains(CommitKey(orderId)))
            return ResultDomain.Error(new MessageItem
                { Code = StockResourceConstants.STOCK_REVERT_WITHOUT_COMMIT });

        Quantity += quantity;
        MarkProcessed(RevertKey(orderId));
        return ResultDomain.Ok();
    }

    // Sweep: suresi gecmis rezervasyonlari sil ve serbest birakilanlari dondur (event icin).
    /// <summary>Suresi gecmis rezervasyonlari siler ve serbest birakilanlari dondurur (event icin).</summary>
    public ResultDomain<IReadOnlyList<ReservationEntry>> PurgeExpired(DateTimeOffset now)
    {
        var expired = _reservations.Where(r => !r.IsActiveAt(now)).ToList();
        foreach (var e in expired) _reservations.Remove(e);
        return ResultDomain<IReadOnlyList<ReservationEntry>>.Ok(expired);
    }
}

// 012-stock-reservation: rezervasyon TTL'i ve sweep periyodu config'den okunur (FR-010).
// appsettings "Reservations" bolumu; test icin kisaltilabilir.
public sealed class ReservationOptions
{
    public const string SectionName = "Reservations";

    // Sabit TTL varsayilani 15 dk (FR-010). ExpiresAt yenilenmez (FR-010a).
    public TimeSpan Ttl { get; set; } = TimeSpan.FromMinutes(15);

    // Hangfire sweep cron (US4). Varsayilan dakikalik.
    public string SweepCron { get; set; } = "* * * * *";
}