namespace Order.Api.Options;

/// <summary>
/// 072: UCP already-captured dış-sipariş girişinin sandbox ödeme + sentetik kullanıcı config'i (section
/// <c>UcpOrder</c>). Dış platform alıcısının Customer BC'de vault kartı YOK (R7) → UCP siparişleri
/// konfigüre iyzico sandbox instrument'ı ile tahsil edilir. MerchantId onboarding'le eşleşmeli (docker
/// reset onboarding'i siler — [[docker-reset-wipes-gateway-onboarding]]); eksikse charge fail-closed döner.
/// </summary>
public class UcpOrderOption
{
    /// <summary>Mağazanın merchant kimliği (PG charge X-Api-Key bu id üzerinden MerchantKeyClient'tan çözülür).</summary>
    public Guid MerchantId { get; set; }

    /// <summary>Sandbox ödeme aracı vault token'ı (3DS-siz test kartı; gerçek para yok).</summary>
    public string SandboxVaultToken { get; set; } = "";

    /// <summary>UCP siparişlerinin sentetik kullanıcı kimliği (kanal alıcısı gerçek Identity kullanıcısı değil).</summary>
    public Guid SyntheticUserId { get; set; }

    /// <summary>Config tam mı (charge denenebilir mi) — merchantId + token + sentetik kullanıcı dolu.</summary>
    public bool IsConfigured =>
        MerchantId != Guid.Empty && !string.IsNullOrWhiteSpace(SandboxVaultToken) && SyntheticUserId != Guid.Empty;
}