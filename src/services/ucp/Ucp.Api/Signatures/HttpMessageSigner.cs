using System.Text;
using NSec.Cryptography;

namespace Ucp.Api.Signatures;

/// <summary>
/// 072 US3: giden webhook'u mağaza private key'iyle RFC 9421 imzalar (SignatureBaseBuilder'a bağlı).
/// Üç header üretir: Content-Digest (RFC 9530), Signature-Input, Signature. ES256 BCL, Ed25519 NSec.
/// </summary>
public sealed class HttpMessageSigner(UcpSigningOption signing) : ISingletonDependency
{
    public record SignedHeaders(string ContentDigest, string SignatureInput, string Signature);

    public SignedHeaders Sign(string method, string targetUri, byte[] body, long created)
    {
        var digest = ContentDigest.Compute(body);
        var paramsValue = SignatureBaseBuilder.BuildParams(created, signing.KeyId, signing.Algorithm);
        var signatureBase = SignatureBaseBuilder.Build(method, targetUri, digest, paramsValue);

        var sig = signing.Algorithm == UcpSigningKeys.AlgEd25519
            ? SignEd25519(signatureBase, signing.StorePrivateKeyPem)
            : Es256Signature.Sign(signatureBase, signing.StorePrivateKeyPem);

        var signatureHeader = $"{UcpSigningKeys.SignatureLabel}=:{Convert.ToBase64String(sig)}:";
        var inputHeader = $"{UcpSigningKeys.SignatureLabel}={paramsValue}";
        return new SignedHeaders(digest, inputHeader, signatureHeader);
    }

    private static byte[] SignEd25519(string signatureBase, string privateKeyPem)
    {
        using var key = Key.Import(SignatureAlgorithm.Ed25519,
            Encoding.UTF8.GetBytes(privateKeyPem), KeyBlobFormat.PkixPrivateKeyText);
        return SignatureAlgorithm.Ed25519.Sign(key, Encoding.UTF8.GetBytes(signatureBase));
    }
}
