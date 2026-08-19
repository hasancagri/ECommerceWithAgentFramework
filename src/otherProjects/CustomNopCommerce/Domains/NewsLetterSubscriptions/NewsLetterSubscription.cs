namespace CustomNopCommerce.Domains.NewsLetterSubscriptions;

/// <summary>
/// Bülten aboneliği — Messaging bounded context'inin aggregate kökü. Zengin aggregate dersi: abone YAŞAM
/// DÖNGÜSÜ (abone ol → çık → tekrar abone). E-posta başına tekliği handler query ile korur (tek aggregate
/// sınırında görülmez). nopCommerce NewsLetterSubscription paritesi (StoreId/LanguageId çıkarıldı).
/// </summary>
public class NewsLetterSubscription : AggregateRoot
{
    public string Email { get; private set; } = default!;
    public bool Active { get; private set; }

    private NewsLetterSubscription() { }

    /// <summary>Yeni abonelik oluşturur (aktif doğar). E-posta guard'ı + teklik handler'da.</summary>
    /// <remarks>Handler: SubscribeCommandHandler</remarks>
    public static NewsLetterSubscription Create(string email) =>
        new() { Email = email, Active = true };

    /// <summary>Abonelikten çıkar (pasifleştirir). Zaten pasifse reddedilir.</summary>
    /// <remarks>Handler: UnsubscribeCommandHandler</remarks>
    public ResultDomain Unsubscribe()
    {
        if (!Active)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(Active), Code = MessagingResourceConstants.SUBSCRIPTION_ALREADY_INACTIVE });
        Active = false;
        return ResultDomain.Ok();
    }

    /// <summary>Tekrar abone yapar (aktifleştirir).</summary>
    /// <remarks>Handler: SubscribeCommandHandler (mevcut pasif abonelik varsa)</remarks>
    public ResultDomain Reactivate()
    {
        Active = true;
        return ResultDomain.Ok();
    }
}
