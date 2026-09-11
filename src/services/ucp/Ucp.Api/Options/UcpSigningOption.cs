namespace Ucp.Api.Options;

/// <summary>
/// RFC 9421 imza config'i (appsettings section <c>UcpSigning</c>). Mağaza private key'i giden webhook'u
/// imzalar; public key keşif profilinde (JWKS) yayınlanır; platform public key'i gelen istek
/// doğrulamasında kullanılır. Zorlama <see cref="RequireSignatures"/> bayrağıyla (sandbox default kapalı).
/// </summary>
public class UcpSigningOption
{
    /// <summary>
    /// Gelen istek imza zorlaması. <c>true</c> → imzasız/bozuk istek 401; <c>false</c> (sandbox default) →
    /// varsa doğrula, yoksa akış bloke olmaz (FR-011; UCP <c>--require_signatures=false</c> deseni).
    /// </summary>
    public bool RequireSignatures { get; set; } = false;

    /// <summary>İmza algoritması: <c>ecdsa-p256-sha256</c> (ES256) | <c>ed25519</c>.</summary>
    [Required] public string Algorithm { get; set; } = Shared.UcpSigningKeys.AlgEcdsaP256;

    /// <summary>Mağaza anahtar kimliği (<c>kid</c>) — JWKS + <c>Signature-Input</c> keyid'i.</summary>
    [Required] public string KeyId { get; set; } = "";

    /// <summary>Mağaza private key (PEM, PKCS#8) — giden webhook imzası. Yalnız mağazada; profilde YOK.</summary>
    [Required] public string StorePrivateKeyPem { get; set; } = "";

    /// <summary>Mağaza public key (PEM, SPKI) — keşif profili JWKS yayını (yalnız public).</summary>
    [Required] public string StorePublicKeyPem { get; set; } = "";

    /// <summary>
    /// Güvenilen platform public key (PEM, SPKI) — gelen istek doğrulaması. MVP tek trusted platform;
    /// gerçek UCP'de <c>UCP-Agent</c> profil JWKS'inden <c>kid</c> ile çözülür (sonraki feature).
    /// </summary>
    public string PlatformPublicKeyPem { get; set; } = "";

    /// <summary>Güvenilen platform anahtarının <c>kid</c>'i (gelen <c>Signature-Input</c> keyid eşleşmesi).</summary>
    public string PlatformKeyId { get; set; } = "";

    /// <summary><c>created</c> tazelik penceresi (saniye); dışı = replay reddi (doğrulama tarafı).</summary>
    [Range(1, 3600)] public int FreshnessWindowSeconds { get; set; } = 300;
}