using System.Text;
using System.Text.RegularExpressions;
using NSec.Cryptography;

namespace Ucp.Api.Signatures;

public enum SignatureVerification { Valid, Invalid, Missing }

/// <summary>
/// 072 US4: gelen istek RFC 9421 imza doğrulaması. Content-Digest (RFC 9530) gövde bütünlüğü + imza
/// tabanı yeniden kurulur + gönderen public key (profil/Options'tan kid ile) ile doğrulanır. ES256 BCL
/// ECDsa, Ed25519 NSec (BCL'de yok). created tazelik penceresi = replay guard. Zorlama middleware'de
/// (RequireSignatures bayrağı); bu sınıf saf doğrulama kararı verir.
/// </summary>
public sealed class HttpMessageSignatureVerifier(UcpSigningOption signing) : ISingletonDependency
{
    private static readonly Regex CreatedRx = new(@"created=(\d+)", RegexOptions.Compiled);
    private static readonly Regex KeyIdRx = new("keyid=\"([^\"]+)\"", RegexOptions.Compiled);
    private static readonly Regex AlgRx = new("alg=\"([^\"]+)\"", RegexOptions.Compiled);

    public SignatureVerification Verify(
        string method, string targetUri, string? signatureInput, string? signature, string? contentDigest,
        byte[] body, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(signatureInput) || string.IsNullOrWhiteSpace(signature)
            || string.IsNullOrWhiteSpace(contentDigest))
            return SignatureVerification.Missing;

        // 1) Content-Digest gövdeyle tutarlı mı (bütünlük).
        if (!string.Equals(contentDigest.Trim(), ContentDigest.Compute(body), StringComparison.Ordinal))
            return SignatureVerification.Invalid;

        // 2) Signature-Input parametrelerini çöz.
        var createdM = CreatedRx.Match(signatureInput);
        var keyIdM = KeyIdRx.Match(signatureInput);
        var algM = AlgRx.Match(signatureInput);
        if (!createdM.Success || !keyIdM.Success || !algM.Success)
            return SignatureVerification.Invalid;

        var created = long.Parse(createdM.Groups[1].Value);
        var keyId = keyIdM.Groups[1].Value;
        var alg = algM.Groups[1].Value;

        // 3) Tazelik (replay guard).
        var age = Math.Abs((now.ToUnixTimeSeconds() - created));
        if (age > signing.FreshnessWindowSeconds)
            return SignatureVerification.Invalid;

        // 4) Güvenilen platform anahtarı (kid eşleşmeli).
        if (!string.Equals(keyId, signing.PlatformKeyId, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(signing.PlatformPublicKeyPem))
            return SignatureVerification.Invalid;

        // 5) İmza tabanını yeniden kur (signer ile aynı bileşen sırası/parametre değeri).
        var paramsValue = SignatureBaseBuilder.BuildParams(created, keyId, alg);
        var signatureBase = SignatureBaseBuilder.Build(method, targetUri, contentDigest.Trim(), paramsValue);

        // 6) İmza baytlarını çöz (sig1=:base64:).
        var sigBytes = ExtractSignatureBytes(signature);
        if (sigBytes is null) return SignatureVerification.Invalid;

        var ok = alg switch
        {
            UcpSigningKeys.AlgEcdsaP256 => Es256Signature.Verify(signatureBase, sigBytes, signing.PlatformPublicKeyPem),
            UcpSigningKeys.AlgEd25519 => VerifyEd25519(signatureBase, sigBytes, signing.PlatformPublicKeyPem),
            _ => false
        };
        return ok ? SignatureVerification.Valid : SignatureVerification.Invalid;
    }

    private static byte[]? ExtractSignatureBytes(string signatureHeader)
    {
        var start = signatureHeader.IndexOf(":", StringComparison.Ordinal);
        var end = signatureHeader.LastIndexOf(":", StringComparison.Ordinal);
        if (start < 0 || end <= start) return null;
        var b64 = signatureHeader.Substring(start + 1, end - start - 1);
        try { return Convert.FromBase64String(b64); }
        catch (FormatException) { return null; }
    }

    private static bool VerifyEd25519(string signatureBase, byte[] signature, string publicKeyPem)
    {
        try
        {
            var pub = PublicKey.Import(SignatureAlgorithm.Ed25519,
                Encoding.UTF8.GetBytes(publicKeyPem), KeyBlobFormat.PkixPublicKeyText);
            return SignatureAlgorithm.Ed25519.Verify(pub, Encoding.UTF8.GetBytes(signatureBase), signature);
        }
        catch
        {
            return false;
        }
    }
}
