namespace CustomNopCommerce.Domains.TaxRates;

/// <summary>
/// Vergi oranı — belirli bir vergi kategorisi (ve isteğe bağlı ülke) için yüzde. Tax bounded context'inin
/// aggregate kökü. nopCommerce'te oran rate-provider plugin'lerinde tutulur; burada öğrenme için domaine
/// alındı. TaxCategoryId (Tax BC içi) + CountryId (Directory BC, opak) referans. Hesap saf metotta.
/// </summary>
public class TaxRate : AggregateRoot
{
    public Guid TaxCategoryId { get; private set; }
    // Vergi ülkeye göre değişir; null = tüm ülkeler. Ülke Directory BC'nin — opak Id.
    public Guid? CountryId { get; private set; }
    public decimal Percentage { get; private set; }

    private TaxRate() { }

    /// <summary>Yeni vergi oranı oluşturur. Yüzde 0-100 guard'ı handler'da.</summary>
    /// <remarks>Handler: CreateTaxRateCommandHandler</remarks>
    public static TaxRate Create(Guid taxCategoryId, Guid? countryId, decimal percentage) =>
        new() { TaxCategoryId = taxCategoryId, CountryId = countryId, Percentage = percentage };

    /// <summary>Verilen tutar için vergi miktarını hesaplar (tutar × yüzde / 100). Saf hesap — durum değiştirmez.</summary>
    public decimal CalculateTax(decimal amount) => amount * Percentage / 100m;
}
