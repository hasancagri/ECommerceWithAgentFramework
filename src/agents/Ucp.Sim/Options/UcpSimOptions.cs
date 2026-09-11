namespace Ucp.Sim.Options;

/// <summary>
/// Platform SİMÜLATÖRÜ config'i (section <c>UcpSim</c>) — mağazanın /ucp cephesini dış AI platformu
/// rolüyle sürer. Token client_credentials (ucp-platform); istekleri mağaza checkout uçlarına gönderir.
/// </summary>
public class UcpSimOptions
{
    /// <summary>Mağaza UCP cephesi taban adresi (Aspire service discovery, ör. http://ucp-api).</summary>
    public string StoreBaseUrl { get; set; } = "http://ucp-api";

    /// <summary>Identity.Server adresi (client_credentials token).</summary>
    public string IdentityAddress { get; set; } = "https://localhost:5001";

    public string ClientId { get; set; } = "ucp-platform";
    public string ClientSecret { get; set; } = "ucp-platform-secret";
    public string Scope { get; set; } = "dev.ucp.shopping.checkout";

    /// <summary>Giden istekleri RFC 9421 ile imzala (mağaza RequireSignatures=on yolunu test etmek için).</summary>
    public bool SignRequests { get; set; } = false;

    /// <summary>Platform (sim) imza anahtar kimliği — mağaza PlatformKeyId ile eşleşmeli.</summary>
    public string KeyId { get; set; } = "sim-ec-1";

    /// <summary>Sim private key (PEM, PKCS#8) — giden istek imzası (ES256).</summary>
    public string PrivateKeyPem { get; set; } = "";

    public string Algorithm { get; set; } = "ecdsa-p256-sha256";

    /// <summary>Mağaza public key (PEM) — gelen webhook imzasını doğrulamak için (store signer).</summary>
    public string StorePublicKeyPem { get; set; } = "";

    /// <summary>Mağaza anahtar kimliği (gelen webhook Signature-Input keyid eşleşmesi).</summary>
    public string StoreKeyId { get; set; } = "store-ec-1";

    /// <summary>Mağazanın webhook'u imzalarken kullandığı kanonik inbox URL'i (@target-uri eşleşmeli).</summary>
    public string InboxUrl { get; set; } = "http://ucp-sim/inbox";
}