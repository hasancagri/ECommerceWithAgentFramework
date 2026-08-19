using CustomNopCommerce.Domains.Discounts.ValueObjects;

namespace CustomNopCommerce.Domains.Discounts;

/// <summary>
/// İndirim — Pricing bounded context'inin aggregate kökü. Zengin aggregate dersleri: (1) saf hesap metodu
/// <see cref="CalculateDiscount"/> (yüzde/sabit + üst sınır + taban aşımı guard'ı); (2) geçerlilik kararı
/// <see cref="IsValidAt"/> (aktiflik + tarih penceresi + kupon); (3) kullanım limiti invariant'ı
/// <see cref="RecordUsage"/> (NTimesOnly / NTimesPerCustomer). Aktiflik için AggregateRoot.IsActive kullanılır.
/// nopCommerce Discount + DiscountUsageHistory paritesi (product/category mapping'leri tüketen BC'de eşleşir).
/// </summary>
public class Discount : AggregateRoot
{
    public string Name { get; private set; } = default!;
    public DiscountType Type { get; private set; }
    public DiscountValue Value { get; private set; } = default!;

    public DateTime? StartDateUtc { get; private set; }
    public DateTime? EndDateUtc { get; private set; }

    public bool RequiresCouponCode { get; private set; }
    public string? CouponCode { get; private set; }
    public bool IsCumulative { get; private set; }

    public DiscountLimitationType Limitation { get; private set; }
    public int LimitationTimes { get; private set; }
    public int? MaximumDiscountedQuantity { get; private set; }

    private readonly List<DiscountUsage> _usages = new();
    public IReadOnlyList<DiscountUsage> Usages => _usages;

    private Discount() { }

    /// <summary>Yeni indirim oluşturur (aktif doğar). Ad/değer guard'ı handler'da.</summary>
    /// <remarks>Handler: CreateDiscountCommandHandler</remarks>
    public static Discount Create(string name, DiscountType type, DiscountValue value,
        DateTime? startDateUtc, DateTime? endDateUtc, bool requiresCouponCode, string? couponCode,
        bool isCumulative, DiscountLimitationType limitation, int limitationTimes, int? maximumDiscountedQuantity)
    {
        return new Discount
        {
            Name = name,
            Type = type,
            Value = value,
            StartDateUtc = startDateUtc,
            EndDateUtc = endDateUtc,
            RequiresCouponCode = requiresCouponCode,
            CouponCode = couponCode,
            IsCumulative = isCumulative,
            Limitation = limitation,
            LimitationTimes = limitationTimes,
            MaximumDiscountedQuantity = maximumDiscountedQuantity,
        };
    }

    /// <summary>Verilen taban tutar için indirim miktarını hesaplar. Yüzdeyse taban×%; sabitse Amount.
    /// Üst sınır (MaximumAmount) uygulanır; indirim tabanı asla aşamaz. Saf hesap — durum değiştirmez.</summary>
    public decimal CalculateDiscount(decimal baseAmount)
    {
        var discount = Value.UsePercentage ? baseAmount * Value.Percentage / 100m : Value.Amount;
        if (Value.MaximumAmount is { } max && discount > max)
            discount = max;
        if (discount > baseAmount)
            discount = baseAmount;
        return discount;
    }

    /// <summary>İndirim şu an + verilen kuponla geçerli mi? Aktiflik + tarih penceresi + kupon eşleşmesi.
    /// Saf sorgu — durum değiştirmez.</summary>
    public bool IsValidAt(DateTime nowUtc, string? providedCoupon)
    {
        if (!IsActive)
            return false;
        if (StartDateUtc is { } start && nowUtc < start)
            return false;
        if (EndDateUtc is { } end && nowUtc > end)
            return false;
        if (RequiresCouponCode && !string.Equals(CouponCode, providedCoupon, StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    /// <summary>İndirimi bir kullanım olarak kaydeder. Kullanım limiti aşılıyorsa reddedilir (invariant).</summary>
    /// <remarks>Handler: RecordDiscountUsageCommandHandler</remarks>
    public ResultDomain RecordUsage(Guid orderId, Guid customerId, DateTime usedAtUtc)
    {
        if (Limitation == DiscountLimitationType.NTimesOnly && _usages.Count >= LimitationTimes)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(Limitation), Code = PricingResourceConstants.DISCOUNT_LIMIT_REACHED });
        if (Limitation == DiscountLimitationType.NTimesPerCustomer
            && _usages.Count(u => u.CustomerId == customerId) >= LimitationTimes)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(Limitation), Code = PricingResourceConstants.DISCOUNT_LIMIT_REACHED });

        _usages.Add(DiscountUsage.Create(orderId, customerId, usedAtUtc));
        return ResultDomain.Ok();
    }

    /// <summary>İndirimi aktifleştirir/pasifleştirir.</summary>
    /// <remarks>Handler: (ileride ToggleDiscount)</remarks>
    public ResultDomain SetActive(bool active)
    {
        IsActive = active;
        return ResultDomain.Ok();
    }
}
