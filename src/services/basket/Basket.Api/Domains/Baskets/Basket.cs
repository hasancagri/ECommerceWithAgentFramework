namespace Basket.Api.Domains.Baskets;

public class Basket : AggregateRoot
{
    private Basket()
    {
    }

    /// <summary>Verilen kullanici icin bos yeni bir sepet olusturur.</summary>
    /// <remarks>Handler: AddBasketItemCommandHandler, AddBasketItemCommandHandler (Agent)</remarks>
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
    /// <summary>Rezervasyon capasinin verilen ana gore dolup dolmadigini doner (bos/capasiz sepette false).</summary>
    /// <remarks>Handler: GetBasketQueryHandler, GetBasketQueryHandler (Agent)</remarks>
    public bool IsExpiredAt(DateTimeOffset now) =>
        _items.Count > 0 && ReservationExpiresAt is not null && ReservationExpiresAt <= now;

    // 017 (FR-002): capayi kurar. Yalniz capa yokken cagrilir; ekleme/adet/silme capaya DOKUNMAZ (FR-003).
    /// <summary>Sepet rezervasyon bitis capasini verilen ana kurar.</summary>
    /// <remarks>Handler: AddBasketItemCommandHandler</remarks>
    public ResultDomain StartReservation(DateTimeOffset expiresAt)
    {
        ReservationExpiresAt = expiresAt;
        return ResultDomain.Ok();
    }

    // 017 (FR-008): sure dolmussa TUM satirlari dusur + capayi sifirla (tembel temizlik); aksi halde no-op.
    /// <summary>Sure dolmussa tum satirlari siler ve capayi sifirlar; aksi halde no-op.</summary>
    /// <remarks>Handler: AddBasketItemCommandHandler, ClearExpiredBasketCommandHandler</remarks>
    public ResultDomain PurgeExpiredItems(DateTimeOffset now)
    {
        if (!IsExpiredAt(now)) return ResultDomain.Ok();
        _items.Clear();
        ReservationExpiresAt = null;
        return ResultDomain.Ok();
    }
    /// <summary>Sepetteki tum satirlarin fiyat*adet toplamini doner.</summary>
    /// <remarks>Handler: GetBasketQueryHandler, GetBasketQueryHandler (Agent)</remarks>
    public decimal GetTotalPrice()
    {
        return _items.Sum(x => x.Price * x.Quantity);
    }

    /// <summary>Ayni Id'li satiri degistirip sepete kalemi ekler (upsert).</summary>
    /// <remarks>Handler: AddBasketItemCommandHandler (Agent)</remarks>
    public ResultDomain AddItem(BasketItem item)
    {
        var existing = _items.FirstOrDefault(x => x.Id == item.Id);
        if (existing is not null)
            _items.Remove(existing);

        _items.Add(item);
        return ResultDomain.Ok();
    }

    // 012: bir urunun sepetteki mevcut adedi (handler yeni rezervasyon adedini hesaplarken kullanir).
    /// <summary>Verilen urunun sepetteki mevcut adedini doner (yoksa 0).</summary>
    /// <remarks>Handler: AddBasketItemCommandHandler</remarks>
    public int GetItemQuantity(Guid productId) =>
        _items.FirstOrDefault(x => x.Id == productId)?.Quantity ?? 0;

    // 012: urunu verilen mutlak adede getirir (upsert). Rezervasyon Stock'ta kararlastirildiktan sonra
    // handler bunu cagirir; ayna model (sepet adedi = rezervasyon adedi). Bitis artik sepet capasinda (017).
    /// <summary>Urunu verilen mutlak adede getirir (upsert) ve son bilinen kalan stogu saklar.</summary>
    /// <remarks>Handler: AddBasketItemCommandHandler, SetBasketItemQuantityCommandHandler</remarks>
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

    /// <summary>Satiri siler; son satirsa capayi sifirlar. Yoksa NotFound doner.</summary>
    /// <remarks>Handler: BasketEventHandlers, SetBasketItemQuantityCommandHandler, DeleteBasketItemCommandHandler, DeleteBasketItemCommandHandler (Agent)</remarks>
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