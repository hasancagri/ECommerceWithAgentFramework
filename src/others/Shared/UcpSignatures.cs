using System.Security.Cryptography;
using System.Text;

namespace Shared;

/// <summary>
/// 072: RFC 9421 imza tabanı (signature base) kurucusu — SAF. Signer ve verifier AYNI tabanı üretmeli
/// (mint↔verify simetrisi; [[UcpSigningKeys]] bileşen sırası). İki-taraf sözleşme olduğundan Shared'da
/// (Ucp.Api verifier/signer + Ucp.Sim signer/verifier aynı kodu kullanır).
/// </summary>
public static class SignatureBaseBuilder
{
    /// <summary>
    /// <c>Signature-Input</c> parametre değeri: <c>("@method" "@target-uri" "content-digest");created=..;
    /// keyid="..";alg=".."</c>. Bu değer hem header'a yazılır hem @signature-params satırına girer.
    /// </summary>
    public static string BuildParams(long created, string keyId, string alg)
    {
        var components = string.Join(" ", UcpSigningKeys.CoveredComponents.Select(c => $"\"{c}\""));
        return $"({components});created={created};keyid=\"{keyId}\";alg=\"{alg}\"";
    }

    /// <summary>
    /// İmza tabanını kurar (RFC 9421 §2.3): kapsanan her bileşen bir satır + son satır @signature-params.
    /// Bileşen değerleri: @method (BÜYÜK harf), @target-uri (tam URI), content-digest (header değeri).
    /// </summary>
    public static string Build(string method, string targetUri, string contentDigest, string paramsValue)
    {
        var sb = new StringBuilder();
        sb.Append("\"@method\": ").Append(method.ToUpperInvariant()).Append('\n');
        sb.Append("\"@target-uri\": ").Append(targetUri).Append('\n');
        sb.Append("\"content-digest\": ").Append(contentDigest).Append('\n');
        sb.Append("\"@signature-params\": ").Append(paramsValue);
        return sb.ToString();
    }
}

/// <summary>072: RFC 9530 Content-Digest (SHA-256) — gövde bütünlüğü. Format: <c>sha-256=:&lt;base64&gt;:</c>.</summary>
public static class ContentDigest
{
    public static string Compute(byte[] body)
        => $"{UcpSigningKeys.ContentDigestAlgorithm}=:{Convert.ToBase64String(SHA256.HashData(body))}:";
}

/// <summary>
/// 072: ecdsa-p256-sha256 (ES256) imza/doğrulama — BCL ECDsa, ham r||s (IEEE P1363; RFC 9421 alg formatı).
/// İki-taraf paylaşımlı (Shared); Ed25519 NSec ile Ucp.Api verifier'da (BCL'de yok).
/// </summary>
public static class Es256Signature
{
    public static byte[] Sign(string signatureBase, string privateKeyPem)
    {
        using var ec = ECDsa.Create();
        ec.ImportFromPem(privateKeyPem);
        return ec.SignData(Encoding.UTF8.GetBytes(signatureBase), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    public static bool Verify(string signatureBase, byte[] signature, string publicKeyPem)
    {
        try
        {
            using var ec = ECDsa.Create();
            ec.ImportFromPem(publicKeyPem);
            return ec.VerifyData(Encoding.UTF8.GetBytes(signatureBase), signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
