namespace CustomNopCommerce.Domains.GdprConsents;

/// <summary>
/// GDPR rıza tanımı (ör. "Pazarlama e-postalarını kabul ediyorum") — Gdpr bounded context'inin aggregate
/// kökü. Kayıt/profil sayfasında müşteriye gösterilir; müşterinin kabul/ret kararı GdprLogEntry'ye denetim
/// kaydı olarak düşer. nopCommerce GdprConsent paritesi.
/// </summary>
public class GdprConsent : AggregateRoot
{
    public string Message { get; private set; } = default!;
    public bool IsRequired { get; private set; }
    public string? RequiredMessage { get; private set; }
    public bool DisplayDuringRegistration { get; private set; }
    public bool DisplayOnCustomerInfoPage { get; private set; }
    public int DisplayOrder { get; private set; }

    private GdprConsent() { }

    /// <summary>Yeni rıza tanımı oluşturur. Mesaj guard'ı handler'da.</summary>
    /// <remarks>Handler: CreateGdprConsentCommandHandler</remarks>
    public static GdprConsent Create(string message, bool isRequired, string? requiredMessage,
        bool displayDuringRegistration, bool displayOnCustomerInfoPage, int displayOrder) =>
        new()
        {
            Message = message,
            IsRequired = isRequired,
            RequiredMessage = requiredMessage,
            DisplayDuringRegistration = displayDuringRegistration,
            DisplayOnCustomerInfoPage = displayOnCustomerInfoPage,
            DisplayOrder = displayOrder,
        };

    /// <summary>Rıza mesajını günceller.</summary>
    /// <remarks>Handler: (ileride UpdateGdprConsent)</remarks>
    public ResultDomain UpdateMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(message), Code = GdprResourceConstants.CONSENT_MESSAGE_REQUIRED });
        Message = message;
        return ResultDomain.Ok();
    }
}
