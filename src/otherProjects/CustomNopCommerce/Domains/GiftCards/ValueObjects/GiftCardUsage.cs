using CustomNopCommerce.Domains.Orders.ValueObjects;

namespace CustomNopCommerce.Domains.GiftCards.ValueObjects;

/// <summary>
/// Hediye kartından tek bir kullanım (bir siparişte harcanan tutar). nopCommerce GiftCardUsageHistory
/// paritesi. GiftCard aggregate'inin child'ı; bakiye bu kullanımlardan TÜRETİLİR (ayrı alan tutulmaz).
/// </summary>
public record GiftCardUsage
{
    public Money AmountUsed { get; private init; } = Money.Zero();
    public Guid? OrderId { get; private init; }
    public DateTime UsedAtUtc { get; private init; }

    private GiftCardUsage() { }

    public static GiftCardUsage Create(Money amountUsed, Guid? orderId, DateTime usedAtUtc) =>
        new() { AmountUsed = amountUsed, OrderId = orderId, UsedAtUtc = usedAtUtc };
}
