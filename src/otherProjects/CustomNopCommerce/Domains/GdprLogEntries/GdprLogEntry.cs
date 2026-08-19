namespace CustomNopCommerce.Domains.GdprLogEntries;

/// <summary>
/// GDPR denetim kaydı — Gdpr bounded context'inin aggregate kökü. APPEND-ONLY: oluşturulur, ASLA değiştirilmez
/// veya silinmez (uyum/denetim değişmezliği — davranış metodu yok, yalnız factory). Kişi başına çok kayıt olur;
/// her biri bir GDPR olayını (rıza kabul/ret, veri dışa aktarım/silme talebi, profil değişimi) zaman damgasıyla
/// dondurur. CustomerId + ConsentId opak referans. nopCommerce GdprLog paritesi.
/// </summary>
public class GdprLogEntry : AggregateRoot
{
    public Guid CustomerId { get; private set; }
    public Guid? ConsentId { get; private set; }
    public GdprRequestType RequestType { get; private set; }
    public string? RequestDetails { get; private set; }
    // Kayıt anındaki müşteri bilgisi anlık görüntüsü (e-posta vb.) — sonradan müşteri değişse de denetim sabit.
    public string? CustomerInfo { get; private set; }

    private GdprLogEntry() { }

    /// <summary>Yeni denetim kaydı oluşturur. Değiştirilemez — mutasyon metodu YOKTUR (append-only).</summary>
    /// <remarks>Handler: RecordGdprActionCommandHandler</remarks>
    public static GdprLogEntry Create(Guid customerId, Guid? consentId, GdprRequestType requestType,
        string? requestDetails, string? customerInfo) =>
        new()
        {
            CustomerId = customerId,
            ConsentId = consentId,
            RequestType = requestType,
            RequestDetails = requestDetails,
            CustomerInfo = customerInfo,
        };
}
