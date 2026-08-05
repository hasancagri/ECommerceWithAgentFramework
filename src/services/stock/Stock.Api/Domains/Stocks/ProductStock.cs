using StockReservation = Stock.Api.Domains.Stocks.Entities.StockReservation;

namespace Stock.Api.Domains.Stocks;

public class ProductStock : AggregateRoot
{
    public Guid ProductId { get; private set; }

    // 012: kalici alan adi Quantity (OnHand = fiziksel stok). Feed/Commit/restock disinda degismez.
    public int Quantity { get; private set; }

    // Domain OnHand semantigi (I1); ayni deger, okunabilirlik icin.
    [JsonIgnore] public int OnHand => Quantity;

    // 012: aktif rezervasyonlar (gomulu entity). Available bunlardan turetilir.
    [JsonProperty("Reservations")] private List<StockReservation> _reservations = new();
    [JsonIgnore] public IReadOnlyList<StockReservation> Reservations => _reservations.AsReadOnly();

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

    public static ProductStock Create(Guid productId, int quantity)
    {
        return new ProductStock
        {
            ProductId = productId,
            Quantity = quantity
        };
    }

    public void Increase(int amount)
    {
        Quantity += amount;
    }

    public void Decrease(int amount)
    {
        Quantity -= amount;
    }

    // 005-supplier-ingestion: feed mutlak adet verir; set semantigi Increase/Decrease'ten ayridir.
    // Invariant: stok adedi negatif olamaz — kural handler'da degil aggregate'te korunur.
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
    public int ReservedAt(DateTimeOffset now) => ActiveReservedQuantity(now);

    // Available = OnHand - aktif rezervasyonlar; oversell'de 0'a kirpilir (G1/FR-017).
    public int AvailableAt(DateTimeOffset now) => Math.Max(0, Quantity - ActiveReservedQuantity(now));

    // Oversell tespiti: tedarikci OnHand'i aktif rezervasyonlarin altina dusurmus olabilir.
    // Log'lama aggregate'te degil handler'da yapilir (aggregate saf kalir).
    public bool IsOversoldAt(DateTimeOffset now) => Quantity < ActiveReservedQuantity(now);

    // Sepete ekleme/artirma: kullanicinin bu urun icin rezervasyonunu mutlak adede getirir.
    // Idempotent (ayna model, FR-011). Sabit TTL: ExpiresAt yalniz ilk olusumda atanir (FR-010a).
    // 017: expiresAt (sepet capasi) verilmisse HER durumda uygulanir — yeni rezervasyon onunla dogar,
    // mevcut rezervasyonun bitisi ona esitlenir (cagiran sabit capayi gecirir; rolling-TTL riski yok).
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
            _reservations.Add(new StockReservation(userId, quantity, expiresAt ?? now.Add(ttl)));
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
    public ResultDomain Release(Guid userId)
    {
        var existing = _reservations.FirstOrDefault(r => r.UserId == userId);
        if (existing is not null) _reservations.Remove(existing);
        return ResultDomain.Ok();
    }

    // Siparis: gecerli rezervasyonu kalici stok dususune cevir (OnHand duser, hold kapanir).
    // 028: orderId idempotency anahtari — ayni siparisin mukerrer Commit'i no-op basari doner.
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
    public IReadOnlyList<StockReservation> PurgeExpired(DateTimeOffset now)
    {
        var expired = _reservations.Where(r => !r.IsActiveAt(now)).ToList();
        foreach (var e in expired) _reservations.Remove(e);
        return expired;
    }
}