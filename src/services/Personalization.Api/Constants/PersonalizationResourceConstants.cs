namespace Personalization.Api.Constants;

// Personalization context'ine ozel hata kodu sabitleri (Result pattern: Code kaynak sabitidir).
public static class PersonalizationResourceConstants
{
    // Satin-alma sinyali kalemsiz olamaz (en az 1 kalem).
    public const string PURCHASE_SIGNAL_ITEMS_REQUIRED = "PURCHASE_SIGNAL_ITEMS_REQUIRED";

    // Kalem adedi > 0 olmali.
    public const string PURCHASE_SIGNAL_QUANTITY_INVALID = "PURCHASE_SIGNAL_QUANTITY_INVALID";

    // Kalem birim tutari >= 0 olmali.
    public const string PURCHASE_SIGNAL_UNIT_PRICE_INVALID = "PURCHASE_SIGNAL_UNIT_PRICE_INVALID";

    // OrderId/UserId bos Guid olamaz (opak referans zorunlu).
    public const string PURCHASE_SIGNAL_REFERENCE_INVALID = "PURCHASE_SIGNAL_REFERENCE_INVALID";

    // Gezinme sinyali tipi bilinen kumede degil.
    public const string BEHAVIOR_SIGNAL_EVENT_TYPE_INVALID = "BEHAVIOR_SIGNAL_EVENT_TYPE_INVALID";

    // AnonymousId/SessionId bos Guid olamaz (telemetri kimlik alanlari zorunlu).
    public const string BEHAVIOR_SIGNAL_IDENTITY_REQUIRED = "BEHAVIOR_SIGNAL_IDENTITY_REQUIRED";
}