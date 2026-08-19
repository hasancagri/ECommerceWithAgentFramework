namespace CustomNopCommerce.Domains.CheckoutAttributes;

/// <summary>
/// Checkout özniteliği — sepetin TAMAMINA sorulan seçim (ör. "Hediye paketi?", "Teslimat notu"). ProductAttribute'a
/// benzer ama ürüne değil siparişe bağlıdır ve Ordering BC'sindedir. Seçilebilir değerleri child koleksiyonda
/// tutar. nopCommerce CheckoutAttribute + CheckoutAttributeValue paritesi (validation/store/tax alanları sadeleşti).
/// </summary>
public class CheckoutAttribute : AggregateRoot
{
    public string Name { get; private set; } = default!;
    public string? TextPrompt { get; private set; }
    public bool IsRequired { get; private set; }
    public bool ShippableProductRequired { get; private set; }
    public CheckoutAttributeControlType ControlType { get; private set; }
    public int DisplayOrder { get; private set; }

    private readonly List<CheckoutAttributeValue> _values = new();
    public IReadOnlyList<CheckoutAttributeValue> Values => _values;

    private CheckoutAttribute() { }

    /// <summary>Yeni checkout özniteliği oluşturur. Ad zorunluluğu handler'da.</summary>
    /// <remarks>Handler: CreateCheckoutAttributeCommandHandler</remarks>
    public static CheckoutAttribute Create(string name, string? textPrompt, bool isRequired,
        bool shippableProductRequired, CheckoutAttributeControlType controlType, int displayOrder)
    {
        return new CheckoutAttribute
        {
            Name = name,
            TextPrompt = textPrompt,
            IsRequired = isRequired,
            ShippableProductRequired = shippableProductRequired,
            ControlType = controlType,
            DisplayOrder = displayOrder,
        };
    }

    /// <summary>Seçilebilir bir değer ekler ve üretilen değerin Id'sini döner.</summary>
    /// <remarks>Handler: AddCheckoutAttributeValueCommandHandler</remarks>
    public ResultDomain<Guid> AddValue(string name, decimal priceAdjustment, decimal weightAdjustment,
        string? colorSquaresRgb, bool isPreSelected, int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ResultDomain<Guid>.Error(new MessageItem
            { Property = nameof(name), Code = OrderingResourceConstants.CHECKOUT_ATTR_VALUE_NAME_REQUIRED });

        var value = CheckoutAttributeValue.Create(name, priceAdjustment, weightAdjustment,
            colorSquaresRgb, isPreSelected, displayOrder);
        _values.Add(value);
        return ResultDomain<Guid>.Ok(value.Id);
    }
}
