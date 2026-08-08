namespace Basket.Api.Domains.Baskets;

public class Basket : AggregateRoot
{
    private Basket()
    {
    }

    public static Basket Create(Guid userId)
    {
        return new Basket { UserId = userId };
    }

    // 021: bir sepet satirinin sabit ust siniri. Efektif max = min(MaxItemQuantity, adet+kalan stok).
    // Tek otorite: hem yazma reddi (handler) hem arayuz-siniri (GetBasket) bu sabitten turer.
    public const int MaxItemQuantity = 5;

    public Guid UserId { get; private set; }

    [JsonProperty("Items")] private List<BasketItem> _items = new();

    [JsonIgnore] public IReadOnlyList<BasketItem> Items => _items.AsReadOnly();

    // 017: sepet capasi — tek mutlak rezervasyon bitisi (UTC). Null = capa yok (bos/eski sepet).
    // Ilk basarili ekleme kurar (handler basarida cagirir); sepet bosalinca sifirlanir.
    public DateTimeOffset? ReservationExpiresAt { get; private set; }

    // 017 (FR-010): sure gecti mi? Bos sepette / capasiz sepette false.
    public bool IsExpiredAt(DateTimeOffset now) =>
        _items.Count > 0 && ReservationExpiresAt is not null && ReservationExpiresAt <= now;

    // 017 (FR-002): capayi kurar. Yalniz capa yokken cagrilir; ekleme/adet/silme capaya DOKUNMAZ (FR-003).
    public ResultDomain StartReservation(DateTimeOffset expiresAt)
    {
        ReservationExpiresAt = expiresAt;
        return ResultDomain.Ok();
    }

    // 017 (FR-008): sure dolmussa TUM satirlari dusur + capayi sifirla (tembel temizlik); aksi halde no-op.
    public ResultDomain PurgeExpiredItems(DateTimeOffset now)
    {
        if (!IsExpiredAt(now)) return ResultDomain.Ok();
        _items.Clear();
        ReservationExpiresAt = null;
        return ResultDomain.Ok();
    }
    public decimal GetTotalPrice()
    {
        return _items.Sum(x => x.Price * x.Quantity);
    }

    public ResultDomain AddItem(BasketItem item)
    {
        var existing = _items.FirstOrDefault(x => x.Id == item.Id);
        if (existing is not null)
            _items.Remove(existing);

        _items.Add(item);
        return ResultDomain.Ok();
    }

    // 012: bir urunun sepetteki mevcut adedi (handler yeni rezervasyon adedini hesaplarken kullanir).
    public int GetItemQuantity(Guid productId) =>
        _items.FirstOrDefault(x => x.Id == productId)?.Quantity ?? 0;

    // 012: urunu verilen mutlak adede getirir (upsert). Rezervasyon Stock'ta kararlastirildiktan sonra
    // handler bunu cagirir; ayna model (sepet adedi = rezervasyon adedi). Bitis artik sepet capasinda (017).
    public ResultDomain SetItem(Guid id, string name, string? imageUrl, decimal price, int quantity, int availableStock = 0)
    {
        var existing = _items.FirstOrDefault(x => x.Id == id);
        if (existing is null)
        {
            existing = new BasketItem(id, name, imageUrl, price);
            _items.Add(existing);
        }

        existing.SetQuantity(quantity);
        // 021: son bilinen kalan serbest stok — efektif max hesabi icin saklanir.
        existing.SetAvailableStock(availableStock);
        return ResultDomain.Ok();
    }

    public FeatureResultModel RemoveItem(Guid itemId)
    {
        var item = _items.FirstOrDefault(x => x.Id == itemId);
        if (item is null) return FeatureResultModel.NotFound();
        _items.Remove(item);
        // 017 (FR-004): son satir da gittiyse capa sifirlanir (elle silme / ReservationExpired yolu dahil).
        if (_items.Count == 0)
            ReservationExpiresAt = null;
        return FeatureResultModel.Ok();
    }
}

// 017: sepet capasinin suresi (FR-013). Basket politikasi — Stock'un Reservations:Ttl'inden ayri.
public sealed class BasketReservationOptions
{
    public const string SectionName = "Basket";

    // Ilk basarili eklemede kurulan capa suresi. Varsayilan 5 dk.
    public TimeSpan ReservationDuration { get; set; } = TimeSpan.FromMinutes(5);
}