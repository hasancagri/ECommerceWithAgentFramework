using System.Security.Cryptography;

namespace Customer.Api.Domains.MerchantInformations;

/// <summary>
/// Tek kullanımlık, süreli credential-giriş ekran oturumu (078). Agent tool'u üretir, link
/// <c>{PublicBaseUrl}/merchant-credentials/{Token}</c> ile taşınır; başarılı ekran POST'u
/// <c>Consume</c> ile oturumu öldürür. GET tüketmez (form açıp vazgeçmek linki öldürmez, süre öldürür).
/// Yeni link eskisini iptal etmez — süre öldürür (bilinçli basitlik, data-model.md).
/// </summary>
public class CredentialEntrySession : AggregateRoot
{
    private CredentialEntrySession()
    {
    }

    /// <summary>256-bit rastgele, URL-safe (base64url) yetki token'ı — link = yetki (İlke V v1.11.1 istisnası).</summary>
    public string Token { get; private set; } = string.Empty;

    /// <summary>Linki üreten admin (denetim izi için; token izde YER ALMAZ).</summary>
    public Guid RequestedByUserId { get; private set; }

    /// <summary>Oturumun ölüm anı (üretim + LinkLifetime); geçmişse oturum pasif ölüdür, ayrı işaret yok.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Başarılı POST anı; dolu ise oturum tüketilmiştir (tek kullanım).</summary>
    public DateTimeOffset? ConsumedAt { get; private set; }

    /// <summary>Yeni ekran oturumu üretir: 256-bit URL-safe token + yaşam süresi. Boş kullanıcı ya da pozitif olmayan süre RET.</summary>
    public static ResultDomain<CredentialEntrySession> Create(Guid requestedByUserId, TimeSpan lifetime)
    {
        var messages = new List<MessageItem>();

        if (requestedByUserId == Guid.Empty)
            messages.Add(new MessageItem { Property = nameof(RequestedByUserId), Code = CustomerResourceConstants.VALUE_IS_REQUIRED });

        if (lifetime <= TimeSpan.Zero)
            messages.Add(new MessageItem { Property = nameof(ExpiresAt), Code = CustomerResourceConstants.INVALID_VALUE });

        if (messages.Count > 0)
            return ResultDomain<CredentialEntrySession>.Error(messages);

        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        return ResultDomain<CredentialEntrySession>.Ok(new CredentialEntrySession
        {
            Token = Convert.ToBase64String(tokenBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_'),
            RequestedByUserId = requestedByUserId,
            ExpiresAt = DateTimeOffset.UtcNow + lifetime
        });
    }

    /// <summary>Oturumu tüketir (tek kullanım invariant'ı): süresi geçmiş ya da zaten tüketilmişse RET, değilse işaretler.</summary>
    public ResultDomain Consume(DateTimeOffset now)
    {
        if (!IsUsable(now))
        {
            return ResultDomain.Error(new MessageItem
            {
                Property = nameof(Token),
                Code = CustomerResourceConstants.INVALID_OPERATION_ERROR
            });
        }

        ConsumedAt = now;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>Oturum hâlâ kullanılabilir mi (süre dolmamış + tüketilmemiş) — saf getter.</summary>
    public bool IsUsable(DateTimeOffset now) => ConsumedAt is null && now <= ExpiresAt;
}