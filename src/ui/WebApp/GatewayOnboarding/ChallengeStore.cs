using System.Collections.Concurrent;

namespace WebApp.GatewayOnboarding;

/// <summary>
/// DropShop ödeme gateway'ine kayıt (E1) sırasında domain-control challenge değerlerini tutan basit
/// bellek-içi depo. Gateway başvuruda tek-kullanımlık <c>token</c> + beklenen <c>value</c> döner;
/// aday site bunu <c>/.well-known/merchant-challenge/{token}</c> yolunda yayınlar. Dev amaçlı
/// (kalıcılık yok); token→değer eşlemesi kısa ömürlüdür.
/// </summary>
public interface IChallengeStore
{
    void Set(string token, string value);
    string? Get(string token);
}

public sealed class InMemoryChallengeStore : IChallengeStore
{
    private readonly ConcurrentDictionary<string, string> _values = new();

    public void Set(string token, string value) => _values[token] = value;

    public string? Get(string token) => _values.TryGetValue(token, out var v) ? v : null;
}

/// <summary>
/// Aktivasyon sonrası DropShop'un verdiği <c>merchantId</c> + <c>MerchantKey</c> (OAuth client_secret)
/// deposu. MerchantKey Identity aktivasyon sayfasında bir kez gösterilir; insan buraya girer. Dev =
/// bellek-içi; PROD'da secret store (user-secrets/env/vault) — düz config'e ASLA yazma. Charge (G5)
/// bu değerle DropShop <c>connect/token</c>'a gider.
/// </summary>
public interface IMerchantCredentialStore
{
    void Set(Guid merchantId, string merchantKey);
    bool HasCredential { get; }
    Guid? MerchantId { get; }
}

public sealed class InMemoryMerchantCredentialStore : IMerchantCredentialStore
{
    private Guid? _merchantId;
    private string? _key;

    public void Set(Guid merchantId, string merchantKey)
    {
        _merchantId = merchantId;
        _key = merchantKey;
    }

    public bool HasCredential => _merchantId is not null && !string.IsNullOrWhiteSpace(_key);
    public Guid? MerchantId => _merchantId;
}
