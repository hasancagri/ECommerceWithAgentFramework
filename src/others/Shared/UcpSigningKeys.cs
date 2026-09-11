namespace Shared;

/// <summary>
/// 072: UCP RFC 9421 (HTTP Message Signatures) iki-taraf sözleşmesi. Signer (mağaza giden webhook /
/// simülatör giden istek) ile verifier (karşı taraf) AYNI algoritma kimliklerini, imza etiketini ve
/// imza-tabanı bileşen sırasını kullanmak zorunda; mint↔verify simetrisi kırılırsa doğrulama sessizce
/// başarısız olur. Simetri iki projede paylaşıldığından sözleşme Shared'da yaşar (AcpSptToken emsali).
/// Yalnız ortak sözcük dağarcığı; anahtar materyali burada DEĞİL (Options + profil JWKS'i).
/// </summary>
public static class UcpSigningKeys
{
    /// <summary>ECDSA P-256 + SHA-256 (BCL <c>ECDsa</c>). RFC 9421 alg kimliği.</summary>
    public const string AlgEcdsaP256 = "ecdsa-p256-sha256";

    /// <summary>Ed25519 (NSec.Cryptography; BCL'de yok). RFC 9421 alg kimliği.</summary>
    public const string AlgEd25519 = "ed25519";

    /// <summary>Tek imza etiketi (Signature/Signature-Input sözlük anahtarı).</summary>
    public const string SignatureLabel = "sig1";

    /// <summary>RFC 9530 Content-Digest algoritması (gövde bütünlüğü).</summary>
    public const string ContentDigestAlgorithm = "sha-256";

    /// <summary>
    /// İmza tabanına giren HTTP bileşenleri, RFC 9421 §2.3 sırasıyla. Sıra korunur — signer ve verifier
    /// aynı diziyi kullanmazsa imza tabanı uyuşmaz. Gövde bütünlüğü <c>content-digest</c> üzerinden.
    /// </summary>
    public static readonly string[] CoveredComponents = ["@method", "@target-uri", "content-digest"];
}