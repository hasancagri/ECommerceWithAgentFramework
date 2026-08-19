namespace CustomNopCommerce.Domains.TaxCategories;

/// <summary>
/// Vergi kategorisi (ör. "Standart %20", "İndirimli %10", "Sıfır") — Tax bounded context'inin aggregate
/// kökü. Ürün/kargo bu kategoriye Id ile bağlanır; asıl oran <see cref="TaxRates.TaxRate"/>'te (kategori +
/// ülke başına) tutulur. nopCommerce TaxCategory paritesi.
/// </summary>
public class TaxCategory : AggregateRoot
{
    public string Name { get; private set; } = default!;
    public int DisplayOrder { get; private set; }

    private TaxCategory() { }

    /// <summary>Yeni vergi kategorisi oluşturur. Ad guard'ı handler'da.</summary>
    /// <remarks>Handler: CreateTaxCategoryCommandHandler</remarks>
    public static TaxCategory Create(string name, int displayOrder) =>
        new() { Name = name, DisplayOrder = displayOrder };

    /// <summary>Kategori adını değiştirir.</summary>
    /// <remarks>Handler: (ileride UpdateTaxCategory)</remarks>
    public ResultDomain Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(name), Code = TaxResourceConstants.CATEGORY_NAME_REQUIRED });
        Name = name;
        return ResultDomain.Ok();
    }
}
