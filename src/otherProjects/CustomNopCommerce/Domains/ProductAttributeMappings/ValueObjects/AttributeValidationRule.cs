namespace CustomNopCommerce.Domains.ProductAttributeMappings.ValueObjects;

/// <summary>
/// Serbest-metin/dosya kontrol tipleri için doğrulama kuralı (nopCommerce mapping validation alanları).
/// TextBox/Multiline için min/max uzunluk; FileUpload için izinli uzantı + azami boyut. Boş = kural yok.
/// </summary>
public record AttributeValidationRule
{
    public int? MinLength { get; private init; }
    public int? MaxLength { get; private init; }
    public string? FileAllowedExtensions { get; private init; }
    public int? FileMaximumSizeBytes { get; private init; }

    private AttributeValidationRule() { }

    public static AttributeValidationRule Create(int? minLength, int? maxLength,
        string? fileAllowedExtensions, int? fileMaximumSizeBytes) =>
        new()
        {
            MinLength = minLength,
            MaxLength = maxLength,
            FileAllowedExtensions = fileAllowedExtensions,
            FileMaximumSizeBytes = fileMaximumSizeBytes,
        };

    public static AttributeValidationRule None() => new();
}
