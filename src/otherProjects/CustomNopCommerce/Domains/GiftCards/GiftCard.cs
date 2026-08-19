using CustomNopCommerce.Domains.GiftCards.ValueObjects;
using CustomNopCommerce.Domains.Orders.ValueObjects;

namespace CustomNopCommerce.Domains.GiftCards;

/// <summary>
/// Hediye kartı — Ordering BC'nin aggregate kökü. Başlangıç tutarı + kupon kodu taşır; bakiye kullanımlardan
/// TÜRETİLİR (<see cref="Balance"/> = başlangıç − Σ kullanım). Zengin aggregate dersleri: (1) türetilmiş
/// bakiye; (2) aktifleştirme yaşam döngüsü (aktif olmadan harcanamaz); (3) harcama invariant'ı (tutar > 0,
/// bakiyeyi aşamaz). Money = Ordering'in kendi Money'si (aynı BC). nopCommerce GiftCard + GiftCardUsageHistory paritesi.
/// </summary>
public class GiftCard : AggregateRoot
{
    public GiftCardType Type { get; private set; }
    public Money InitialAmount { get; private set; } = Money.Zero();
    public string CouponCode { get; private set; } = default!;
    public bool IsActivated { get; private set; }

    public string? RecipientName { get; private set; }
    public string? RecipientEmail { get; private set; }
    public string? SenderName { get; private set; }
    public string? SenderEmail { get; private set; }
    public string? Message { get; private set; }

    private readonly List<GiftCardUsage> _usages = new();
    public IReadOnlyList<GiftCardUsage> Usages => _usages;

    private GiftCard() { }

    /// <summary>Yeni hediye kartı çıkarır (aktif değil doğar). Tutar/kupon guard'ı handler'da.</summary>
    /// <remarks>Handler: IssueGiftCardCommandHandler</remarks>
    public static GiftCard Create(GiftCardType type, Money initialAmount, string couponCode,
        string? recipientName, string? recipientEmail, string? senderName, string? senderEmail, string? message)
    {
        return new GiftCard
        {
            Type = type,
            InitialAmount = initialAmount,
            CouponCode = couponCode,
            IsActivated = false,
            RecipientName = recipientName,
            RecipientEmail = recipientEmail,
            SenderName = senderName,
            SenderEmail = senderEmail,
            Message = message,
        };
    }

    /// <summary>Kalan bakiye = başlangıç tutarı − toplam kullanım. Türetilir; ayrı alan yok.</summary>
    public Money Balance()
    {
        var used = Money.Zero(InitialAmount.Currency);
        foreach (var usage in _usages)
            used = used.Add(usage.AmountUsed);
        return InitialAmount.Subtract(used);
    }

    /// <summary>Kartı aktifleştirir (harcanabilir hale getirir).</summary>
    /// <remarks>Handler: (ileride ActivateGiftCard)</remarks>
    public ResultDomain Activate()
    {
        IsActivated = true;
        return ResultDomain.Ok();
    }

    /// <summary>Karttan tutar harcar. Kart aktif olmalı, tutar > 0 ve bakiyeyi aşmamalı (invariant).</summary>
    /// <remarks>Handler: RedeemGiftCardCommandHandler</remarks>
    public ResultDomain Redeem(Money amount, Guid? orderId, DateTime usedAtUtc)
    {
        if (!IsActivated)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(IsActivated), Code = OrderingResourceConstants.GIFTCARD_NOT_ACTIVE });
        if (amount.Amount <= 0)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(amount), Code = OrderingResourceConstants.GIFTCARD_REDEEM_AMOUNT_INVALID });
        if (amount.Amount > Balance().Amount)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(amount), Code = OrderingResourceConstants.GIFTCARD_INSUFFICIENT_BALANCE });

        _usages.Add(GiftCardUsage.Create(amount, orderId, usedAtUtc));
        return ResultDomain.Ok();
    }
}
