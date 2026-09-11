using System.Security.Cryptography;
using System.Text;
using NSec.Cryptography;
using Shared;
using Shouldly;
using Ucp.Api.Options;
using Ucp.Api.Signatures;
using Xunit;

namespace Ucp.Api.Tests;

// 072 İlke VI: RFC 9421/9530 imza tabanı + Content-Digest + verifier (ES256 + Ed25519, tazelik) test-first.
public class HttpMessageSignatureTests
{
    private const string Method = "POST";
    private const string Uri = "http://ucp-api/ucp/checkout_sessions";
    private static readonly byte[] Body = Encoding.UTF8.GetBytes("{\"currency\":\"TRY\"}");

    [Fact]
    public void ContentDigest_Format_IsRfc9530()
    {
        var d = ContentDigest.Compute(Body);
        d.ShouldStartWith("sha-256=:");
        d.ShouldEndWith(":");
    }

    [Fact]
    public void SignatureBase_Contains_ComponentsInOrder()
    {
        var pv = SignatureBaseBuilder.BuildParams(1000, "k1", "ecdsa-p256-sha256");
        var b = SignatureBaseBuilder.Build(Method, Uri, "sha-256=:x:", pv);

        b.ShouldContain("\"@method\": POST");
        b.ShouldContain("\"@target-uri\": " + Uri);
        b.ShouldContain("\"content-digest\": sha-256=:x:");
        b.ShouldContain("\"@signature-params\": " + pv);
        // Sıra: method → target-uri → content-digest → signature-params.
        b.IndexOf("@method").ShouldBeLessThan(b.IndexOf("@target-uri"));
        b.IndexOf("@target-uri").ShouldBeLessThan(b.IndexOf("content-digest"));
    }

    [Fact]
    public void Es256_SignVerify_RoundTrip()
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var priv = ec.ExportPkcs8PrivateKeyPem();
        var pub = ec.ExportSubjectPublicKeyInfoPem();

        var baseStr = SignatureBaseBuilder.Build(Method, Uri, ContentDigest.Compute(Body),
            SignatureBaseBuilder.BuildParams(1000, "k1", "ecdsa-p256-sha256"));
        var sig = Es256Signature.Sign(baseStr, priv);

        Es256Signature.Verify(baseStr, sig, pub).ShouldBeTrue();
        Es256Signature.Verify(baseStr + "tamper", sig, pub).ShouldBeFalse();
    }

    [Fact]
    public void Verifier_ValidEs256_ReturnsValid()
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var (input, sig, digest, created) = SignEs256(ec.ExportPkcs8PrivateKeyPem(), "sim-ec-1", Body);
        var verifier = Verifier(ec.ExportSubjectPublicKeyInfoPem(), "sim-ec-1");

        verifier.Verify(Method, Uri, input, sig, digest, Body, Now(created))
            .ShouldBe(SignatureVerification.Valid);
    }

    [Fact]
    public void Verifier_MissingHeaders_ReturnsMissing()
    {
        var verifier = Verifier("x", "sim-ec-1");
        verifier.Verify(Method, Uri, null, null, null, Body, DateTimeOffset.UtcNow)
            .ShouldBe(SignatureVerification.Missing);
    }

    [Fact]
    public void Verifier_TamperedBody_ReturnsInvalid()
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var (input, sig, digest, created) = SignEs256(ec.ExportPkcs8PrivateKeyPem(), "sim-ec-1", Body);
        var verifier = Verifier(ec.ExportSubjectPublicKeyInfoPem(), "sim-ec-1");

        var tampered = Encoding.UTF8.GetBytes("{\"currency\":\"USD\"}");
        verifier.Verify(Method, Uri, input, sig, digest, tampered, Now(created))
            .ShouldBe(SignatureVerification.Invalid);
    }

    [Fact]
    public void Verifier_WrongKeyId_ReturnsInvalid()
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var (input, sig, digest, created) = SignEs256(ec.ExportPkcs8PrivateKeyPem(), "sim-ec-1", Body);
        var verifier = Verifier(ec.ExportSubjectPublicKeyInfoPem(), "different-kid");

        verifier.Verify(Method, Uri, input, sig, digest, Body, Now(created))
            .ShouldBe(SignatureVerification.Invalid);
    }

    [Fact]
    public void Verifier_StaleCreated_ReturnsInvalid()
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var (input, sig, digest, created) = SignEs256(ec.ExportPkcs8PrivateKeyPem(), "sim-ec-1", Body);
        var verifier = Verifier(ec.ExportSubjectPublicKeyInfoPem(), "sim-ec-1");

        // created'dan 10 dk sonra (pencere 300 sn) → replay reddi.
        verifier.Verify(Method, Uri, input, sig, digest, Body, Now(created).AddMinutes(10))
            .ShouldBe(SignatureVerification.Invalid);
    }

    [Fact]
    public void Verifier_ValidEd25519_ReturnsValid()
    {
        using var key = Key.Create(SignatureAlgorithm.Ed25519,
            new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });
        var pubPem = key.PublicKey.Export(KeyBlobFormat.PkixPublicKeyText);
        var pubPemStr = Encoding.UTF8.GetString(pubPem);

        long created = 2000;
        var digest = ContentDigest.Compute(Body);
        var pv = SignatureBaseBuilder.BuildParams(created, "sim-ec-1", "ed25519");
        var baseStr = SignatureBaseBuilder.Build(Method, Uri, digest, pv);
        var sig = SignatureAlgorithm.Ed25519.Sign(key, Encoding.UTF8.GetBytes(baseStr));

        var verifier = Verifier(pubPemStr, "sim-ec-1");
        verifier.Verify(Method, Uri, $"sig1={pv}", $"sig1=:{Convert.ToBase64String(sig)}:", digest, Body, Now(created))
            .ShouldBe(SignatureVerification.Valid);
    }

    private static (string input, string sig, string digest, long created) SignEs256(string privPem, string kid, byte[] body)
    {
        long created = 1000;
        var digest = ContentDigest.Compute(body);
        var pv = SignatureBaseBuilder.BuildParams(created, kid, "ecdsa-p256-sha256");
        var baseStr = SignatureBaseBuilder.Build(Method, Uri, digest, pv);
        var sig = Es256Signature.Sign(baseStr, privPem);
        return ($"sig1={pv}", $"sig1=:{Convert.ToBase64String(sig)}:", digest, created);
    }

    private static HttpMessageSignatureVerifier Verifier(string publicPem, string trustedKid) =>
        new(new UcpSigningOption
        {
            PlatformKeyId = trustedKid,
            PlatformPublicKeyPem = publicPem,
            FreshnessWindowSeconds = 300,
            KeyId = "store",
            StorePrivateKeyPem = "x",
            StorePublicKeyPem = "x"
        });

    private static DateTimeOffset Now(long created) => DateTimeOffset.FromUnixTimeSeconds(created);
}
