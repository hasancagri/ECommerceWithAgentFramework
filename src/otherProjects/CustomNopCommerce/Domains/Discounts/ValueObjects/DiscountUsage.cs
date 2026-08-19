namespace CustomNopCommerce.Domains.Discounts.ValueObjects;

/// <summary>
/// İndirimin tek bir kullanımı (bir sipariş/müşteri tarafından). nopCommerce DiscountUsageHistory paritesi.
/// Discount aggregate'inin child'ı; kullanım limiti (NTimesOnly/PerCustomer) bu kayıtlardan denetlenir.
/// </summary>
public record DiscountUsage
{
    public Guid OrderId { get; private init; }
    public Guid CustomerId { get; private init; }
    public DateTime UsedAtUtc { get; private init; }

    private DiscountUsage() { }

    public static DiscountUsage Create(Guid orderId, Guid customerId, DateTime usedAtUtc) =>
        new() { OrderId = orderId, CustomerId = customerId, UsedAtUtc = usedAtUtc };
}
