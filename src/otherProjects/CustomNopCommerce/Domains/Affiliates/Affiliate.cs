namespace CustomNopCommerce.Domains.Affiliates;

/// <summary>
/// Satıcı-ortağı (affiliate) — Affiliates bounded context'inin aggregate kökü. Bir referral slug'ı
/// (<see cref="FriendlyUrlName"/>) taşır; ziyaretçi bu slug ile gelince sipariş bu ortağa atfedilir.
/// Komisyon burada DEĞİL, Order.AffiliateId opak geri-referansı + raporlarla izlenir (bu BC ortağı tanımlar,
/// hesabı tutmaz). Aktiflik için AggregateRoot.IsActive yeniden kullanılır. Adres opak Id (Directory/Customer).
/// nopCommerce Affiliate paritesi. Bu, Order/Customer god-entity'lerinden çıkarılan AffiliateId'nin hedefi.
/// </summary>
public class Affiliate : AggregateRoot
{
    public string FriendlyUrlName { get; private set; } = default!;
    public Guid? AddressId { get; private set; }
    public string? AdminComment { get; private set; }

    private Affiliate() { }

    /// <summary>Yeni ortak oluşturur (aktif doğar). Slug guard'ı + tekliği handler'da.</summary>
    /// <remarks>Handler: CreateAffiliateCommandHandler</remarks>
    public static Affiliate Create(string friendlyUrlName, Guid? addressId, string? adminComment) =>
        new() { FriendlyUrlName = friendlyUrlName, AddressId = addressId, AdminComment = adminComment };

    /// <summary>Referral slug'ını değiştirir (teklik handler'da denetlenir).</summary>
    /// <remarks>Handler: (ileride UpdateAffiliate)</remarks>
    public ResultDomain SetFriendlyUrlName(string friendlyUrlName)
    {
        if (string.IsNullOrWhiteSpace(friendlyUrlName))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(friendlyUrlName), Code = AffiliatesResourceConstants.URL_REQUIRED });
        FriendlyUrlName = friendlyUrlName;
        return ResultDomain.Ok();
    }

    /// <summary>Ortağı pasifleştirir. Zaten pasifse reddedilir.</summary>
    /// <remarks>Handler: (ileride DeactivateAffiliate)</remarks>
    public ResultDomain Deactivate()
    {
        if (!IsActive)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(IsActive), Code = AffiliatesResourceConstants.ALREADY_INACTIVE });
        IsActive = false;
        return ResultDomain.Ok();
    }
}
