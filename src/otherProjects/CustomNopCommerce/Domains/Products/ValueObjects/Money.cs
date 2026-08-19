namespace CustomNopCommerce.Domains.Products.ValueObjects;

/// <summary>
/// Para değeri (tutar + para birimi). Değeriyle tanımlı value object — kimliği yok.
/// nopCommerce'te fiyatlar çıplak decimal; burada birim taşıyan VO'ya sarılır (invariant: tutar >= 0).
/// </summary>
public record Money
{
    public decimal Amount { get; private init; }
    public string Currency { get; private init; } = "TRY";

    private Money() { }

    /// <summary>Tutar negatifse null döner (guard çağıranda). Aksi halde Money üretir.</summary>
    public static Money? Create(decimal amount, string currency = "TRY")
    {
        if (amount < 0)
            return null;
        return new Money { Amount = amount, Currency = currency };
    }

    public static Money Zero(string currency = "TRY") => new() { Amount = 0, Currency = currency };
}
