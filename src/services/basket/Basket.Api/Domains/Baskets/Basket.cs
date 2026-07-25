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
    // handler bunu cagirir; ayna model (sepet adedi = rezervasyon adedi). expiresAt UI geri sayimi icin.
    public void SetItem(Guid id, string name, string? imageUrl, decimal price, int quantity,
        DateTimeOffset? expiresAt)
    {
        var existing = _items.FirstOrDefault(x => x.Id == id);
        if (existing is null)
        {
            existing = new BasketItem(id, name, imageUrl, price);
            _items.Add(existing);
        }

        existing.SetQuantity(quantity);
        existing.SetReservationExpiresAt(expiresAt);
        if (IsApplyDiscount())
            existing.ApplyDiscount(AppliedDiscount!.Rate);
    }

    public FeatureResultModel RemoveItem(Guid itemId)
    {
        var item = _items.FirstOrDefault(x => x.Id == itemId);
        if (item is null) return FeatureResultModel.NotFound();
        _items.Remove(item);
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