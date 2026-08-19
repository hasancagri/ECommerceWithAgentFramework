namespace CustomNopCommerce.Domains.Orders.ValueObjects;

/// <summary>
/// Para değeri — Ordering BC'nin KENDİ Money'si. Catalog'un Money'siyle aynı kavram ama ayrı model
/// (BC izolasyonu: paylaşımlı domain modeli yok; kod tekrarı bilinçli). Tutar negatif olamaz.
/// </summary>
public record Money
{
    public decimal Amount { get; private init; }
    public string Currency { get; private init; } = "TRY";

    private Money() { }

    public static Money? Create(decimal amount, string currency = "TRY")
    {
        if (amount < 0)
            return null;
        return new Money { Amount = amount, Currency = currency };
    }

    public static Money Zero(string currency = "TRY") => new() { Amount = 0, Currency = currency };

    public Money Add(Money other) => new() { Amount = Amount + other.Amount, Currency = Currency };
    public Money Subtract(Money other) => new() { Amount = Amount - other.Amount, Currency = Currency };
    public Money Multiply(int qty) => new() { Amount = Amount * qty, Currency = Currency };
}
