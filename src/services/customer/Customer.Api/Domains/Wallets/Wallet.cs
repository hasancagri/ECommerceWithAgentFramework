namespace Customer.Api.Domains.Wallets;

// 075: Bir kullanicinin cuzdani (UserId ile keyli, kullanici basina tek). INCE model — kart verisi
// mağazada DEĞİL, PG'de (FR-016). Burada yalnız iki çapa tutulur:
//   PgUserHandle       — PG'deki kart kümesinin kullanıcı-handle'ı (ilk kart eklemede PG döner).
//   DefaultCardHandle  — kullanıcının varsayılan kart tercihi (opak PG kart-handle'ı; ≤1 varsayılan).
// Kart listesi/silme PG'ye canlı gider (aggregate state değil) → handler'da PG client + bu metotlar.
public class Wallet : AggregateRoot
{
    private Wallet() { }

    /// <summary>Yeni bir cuzdan olusturur (verilen UserId ile); handle'lar boştur (tembel oluşum).</summary>
    public static Wallet Create(Guid userId) => new() { UserId = userId };

    public Guid UserId { get; private set; }

    /// <summary>PG kullanıcı-handle'ı — kullanıcının PG'deki kart kümesi. null = hiç kart eklenmemiş.</summary>
    public string? PgUserHandle { get; private set; }

    /// <summary>Varsayılan kartın opak PG kart-handle'ı. ≤1 varsayılan. null = varsayılan yok.</summary>
    public string? DefaultCardHandle { get; private set; }

    /// <summary>PG kullanıcı-handle çapasını kurar. Boş handle reddedilir; zaten varsa DEĞİŞMEZ
    /// (aynı kullanıcı = aynı handle — ikinci ekleme yeni handle yazmaz, idempotent).</summary>
    public ResultDomain SetPgUserHandle(string handle)
    {
        if (string.IsNullOrWhiteSpace(handle))
            return ResultDomain.Error(
                new MessageItem { Property = nameof(PgUserHandle), Code = CustomerResourceConstants.VALUE_IS_REQUIRED });

        // Idempotent: handle bir kez yazılır; sonraki eklemeler aynı kullanıcı-handle'ını korur.
        if (!string.IsNullOrWhiteSpace(PgUserHandle))
            return ResultDomain.Ok();

        PgUserHandle = handle;
        return ResultDomain.Ok();
    }

    /// <summary>Verilen kartı varsayılan yapar (öncekini ezer → tek varsayılan). PgUserHandle olmadan
    /// (hiç kart yokken) reddedilir.</summary>
    public ResultDomain SetDefaultCard(string cardHandle)
    {
        if (string.IsNullOrWhiteSpace(cardHandle))
            return ResultDomain.Error(
                new MessageItem { Property = nameof(DefaultCardHandle), Code = CustomerResourceConstants.VALUE_IS_REQUIRED });

        if (string.IsNullOrWhiteSpace(PgUserHandle))
            return ResultDomain.Error(
                new MessageItem { Code = CustomerResourceConstants.CARD_NOT_FOUND });

        DefaultCardHandle = cardHandle;
        return ResultDomain.Ok();
    }

    /// <summary>Silinen kart varsayılansa varsayılanı temizler (FR-012); eşleşmezse no-op.</summary>
    public ResultDomain ClearDefaultIfMatches(string cardHandle)
    {
        if (DefaultCardHandle is not null && DefaultCardHandle == cardHandle)
            DefaultCardHandle = null;
        return ResultDomain.Ok();
    }

    /// <summary>Varsayılan boşsa verilen kartı varsayılan yapar (FR-001a — ilk kart otomatik
    /// varsayılan); zaten varsayılan varsa dokunmaz.</summary>
    public ResultDomain MarkFirstCardDefault(string cardHandle)
    {
        if (string.IsNullOrWhiteSpace(DefaultCardHandle) && !string.IsNullOrWhiteSpace(cardHandle))
            DefaultCardHandle = cardHandle;
        return ResultDomain.Ok();
    }
}
