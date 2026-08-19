namespace CustomNopCommerce.Domains.ReviewTypes;

/// <summary>
/// Çok-kriterli yorum boyutu (ör. "Kalite", "Fiyat/Performans", "Kargo Hızı"). Yorumda her boyuta ayrı
/// puan verilir (bkz. ProductReview.CriteriaRatings). Küçük aggregate kökü. nopCommerce ReviewType paritesi.
/// </summary>
public class ReviewType : AggregateRoot
{
    public string Name { get; private set; } = default!;
    public string Description { get; private set; } = string.Empty;
    public int DisplayOrder { get; private set; }
    public bool VisibleToAllCustomers { get; private set; }
    public bool IsRequired { get; private set; }

    private ReviewType() { }

    /// <summary>Yeni yorum kriteri oluşturur. Ad zorunluluğu handler'da.</summary>
    /// <remarks>Handler: CreateReviewTypeCommandHandler</remarks>
    public static ReviewType Create(string name, string description, int displayOrder,
        bool visibleToAllCustomers, bool isRequired) =>
        new()
        {
            Name = name,
            Description = description,
            DisplayOrder = displayOrder,
            VisibleToAllCustomers = visibleToAllCustomers,
            IsRequired = isRequired,
        };

    /// <summary>Kriter adını değiştirir.</summary>
    /// <remarks>Handler: (ileride UpdateReviewType)</remarks>
    public ResultDomain Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(name), Code = CatalogResourceConstants.REVIEWTYPE_NAME_REQUIRED });
        Name = name;
        return ResultDomain.Ok();
    }
}
