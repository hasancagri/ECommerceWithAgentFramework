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
    public void StartReservation(DateTimeOffset expiresAt) => ReservationExpiresAt = expiresAt;

    // 017 (FR-008): sure dolmussa TUM satirlari dusur + capayi sifirla (tembel temizlik); aksi halde no-op.
    public void PurgeExpiredItems(DateTimeOffset now)
    {
        if (!IsExpiredAt(now)) return;
        _items.Clear();
        ReservationExpiresAt = null;
    }
    public Discount? AppliedDiscount { get; private set; }
    private bool IsApplyDiscount() => AppliedDiscount is not null;

    public decimal GetTotalPrice()
    {
        return _items.Sum(x => x.Price * x.Quantity);
    }

    public decimal? GetTotalPriceWithAppliedDiscount()
    {
        return !IsApplyDiscount() ? null : _items.Sum(x => x.PriceByApplyDiscountRate * x.Quantity);
    }

    public void AddItem(BasketItem item)
    {
        var existing = _items.FirstOrDefault(x => x.Id == item.Id);
        if (existing is not null)
            _items.Remove(existing);

        _items.Add(item);
        if (IsApplyDiscount())
            item.ApplyDiscount(AppliedDiscount!.Rate);
    }

    // 012: bir urunun sepetteki mevcut adedi (handler yeni rezervasyon adedini hesaplarken kullanir).
    public int GetItemQuantity(Guid productId) =>
        _items.FirstOrDefault(x => x.Id == productId)?.Quantity ?? 0;

    // 012: urunu verilen mutlak adede getirir (upsert). Rezervasyon Stock'ta kararlastirildiktan sonra
    // handler bunu cagirir; ayna model (sepet adedi = rezervasyon adedi). Bitis artik sepet capasinda (017).
    public void SetItem(Guid id, string name, string? imageUrl, decimal price, int quantity)
    {
        var existing = _items.FirstOrDefault(x => x.Id == id);
        if (existing is null)
        {
            existing = new BasketItem(id, name, imageUrl, price);
            _items.Add(existing);
        }

        existing.SetQuantity(quantity);
        if (IsApplyDiscount())
            existing.ApplyDiscount(AppliedDiscount!.Rate);
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

    public FeatureResultModel ApplyNewDiscount(string coupon, float discountRate)
    {
        if (_items.Count == 0)
            return FeatureResultModel.Error(new MessageItem { Code = "BASKET_IS_EMPTY" });
        
        AppliedDiscount = Discount.Create(coupon, discountRate);
        foreach (var item in _items)
            item.ApplyDiscount(discountRate);
        
        return FeatureResultModel.Ok();
    }

    public void ApplyAvailableDiscount()
    {
        if (!IsApplyDiscount()) return;
        foreach (var item in _items)
            item.ApplyDiscount(AppliedDiscount!.Rate);
    }

    public void ClearDiscount()
    {
        AppliedDiscount = null;
        foreach (var item in _items)
            item.ClearDiscount();
    }
}