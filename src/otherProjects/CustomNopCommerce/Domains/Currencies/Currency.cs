namespace CustomNopCommerce.Domains.Currencies;

/// <summary>
/// Para birimi — Directory bounded context'inin aggregate kökü. Birincil para birimine göre kur (<see cref="Rate"/>)
/// taşır; çevrim saf metotta (<see cref="Convert"/>). nopCommerce Currency paritesi (locale/formatting sadeleşti).
/// </summary>
public class Currency : AggregateRoot
{
    public string Name { get; private set; } = default!;
    public string CurrencyCode { get; private set; } = default!;
    // Birincil para birimine göre kur (birincil = 1). Ör. USD birincilse EUR.Rate = 0.92.
    public decimal Rate { get; private set; }
    public bool Published { get; private set; }
    public int DisplayOrder { get; private set; }

    private Currency() { }

    /// <summary>Yeni para birimi oluşturur. Ad/kod/kur guard'ı handler'da.</summary>
    /// <remarks>Handler: CreateCurrencyCommandHandler</remarks>
    public static Currency Create(string name, string currencyCode, decimal rate, bool published, int displayOrder) =>
        new()
        {
            Name = name,
            CurrencyCode = currencyCode,
            Rate = rate,
            Published = published,
            DisplayOrder = displayOrder,
        };

    /// <summary>Kuru günceller. Pozitif olmalı.</summary>
    /// <remarks>Handler: UpdateCurrencyRateCommandHandler</remarks>
    public ResultDomain UpdateRate(decimal rate)
    {
        if (rate <= 0)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(rate), Code = DirectoryResourceConstants.CURRENCY_RATE_INVALID });
        Rate = rate;
        return ResultDomain.Ok();
    }

    /// <summary>Birincil para birimindeki tutarı bu para birimine çevirir (tutar × kur). Saf hesap.</summary>
    public decimal Convert(decimal amountInPrimary) => amountInPrimary * Rate;
}
