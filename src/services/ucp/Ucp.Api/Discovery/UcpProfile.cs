using System.Security.Cryptography;

namespace Ucp.Api.Discovery;

/// <summary>
/// 072 US2: <c>.well-known/ucp</c> business profili (durum değil, sunum). capabilities (checkout) +
/// extensions (fulfillment, discount) + payment_handlers (mağaza/PG ilanı) + keys (public JWKS, RFC 7517).
/// Anahtar materyali Options'tan; yalnız PUBLIC key yayınlanır. Hem giden webhook imzasını hem gelen
/// istek doğrulamasını besleyen doğrulama anahtarı.
/// </summary>
public static class UcpProfile
{
    public static object Build(UcpPlatformOption platform, UcpSigningOption signing)
    {
        return new
        {
            ucp = new
            {
                capabilities = new[] { AuthorizationScopes.UcpCheckout },
                extensions = new[] { "dev.ucp.shopping.fulfillment", "dev.ucp.shopping.discount" },
                payment_handlers = new[]
                {
                    new
                    {
                        spec = platform.PaymentHandlerSpec,
                        available_instruments = new[] { platform.PaymentInstrument }
                    }
                }
            },
            keys = new[] { BuildJwk(signing) }
        };
    }

    // Mağaza public EC key'ini JWK'e çevirir (kty=EC, crv=P-256, x/y base64url). kid = konfigüre KeyId.
    private static object BuildJwk(UcpSigningOption signing)
    {
        using var ec = ECDsa.Create();
        ec.ImportFromPem(signing.StorePublicKeyPem);
        var p = ec.ExportParameters(false);
        return new
        {
            kid = signing.KeyId,
            kty = "EC",
            crv = "P-256",
            x = Base64Url(p.Q.X!),
            y = Base64Url(p.Q.Y!),
            alg = "ES256",
            use = "sig"
        };
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
