namespace WebApp.GatewayOnboarding;

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
