using System.Security.Cryptography;
using System.Text;
using Shared;
using Shouldly;
using Ucp.Api.Options;
using Ucp.Api.Signatures;
using Xunit;

namespace Ucp.Api.Tests;

// 072 İlke VI: giden webhook imzalayıcısı doğru header üretir + verifier ile round-trip (aynı SignatureBaseBuilder).
public class HttpMessageSignerTests
{
    private const string Method = "POST";
    private const string Target = "http://ucp-sim/inbox";
    private static readonly byte[] Body = Encoding.UTF8.GetBytes("{\"type\":\"order.confirmed\"}");

    [Fact]
    public void Sign_ProducesRfcHeaders_ThatVerifierAccepts()
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var priv = ec.ExportPkcs8PrivateKeyPem();
        var pub = ec.ExportSubjectPublicKeyInfoPem();

        var signing = new UcpSigningOption
        {
            KeyId = "store-ec-1",
            Algorithm = UcpSigningKeys.AlgEcdsaP256,
            StorePrivateKeyPem = priv,
            StorePublicKeyPem = pub,
            // Verifier tarafı: mağaza kendi imzasını doğrulayabilsin diye trusted = store key.
            PlatformKeyId = "store-ec-1",
            PlatformPublicKeyPem = pub,
            FreshnessWindowSeconds = 300
        };

        var signer = new HttpMessageSigner(signing);
        long created = 5000;
        var headers = signer.Sign(Method, Target, Body, created);

        headers.ContentDigest.ShouldBe(ContentDigest.Compute(Body));
        headers.SignatureInput.ShouldStartWith("sig1=");
        headers.SignatureInput.ShouldContain("keyid=\"store-ec-1\"");
        headers.Signature.ShouldStartWith("sig1=:");

        var verifier = new HttpMessageSignatureVerifier(signing);
        verifier.Verify(Method, Target, headers.SignatureInput, headers.Signature, headers.ContentDigest,
                Body, DateTimeOffset.FromUnixTimeSeconds(created))
            .ShouldBe(SignatureVerification.Valid);
    }
}
