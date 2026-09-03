namespace Identity.Server.Connect;

// 061: Dış agent (DCR) istemcilerinin kapalı scope demeti + izinli redirect kalıpları.
// Tek kaynak: specs/061-external-mcp-oauth/data-model.md. Yönetim scope'ları buraya GİREMEZ.
public static class ExternalAgentDefaults
{
    // Alışveriş yaşam döngüsü: arama→sepet→sipariş→takip + profil/ödeme okuma.
    public static readonly string[] ApiScopes =
    [
        "storefront.read",
        "basket.read", "basket.write",
        "order.read", "order.write",
        "customer.read",
        "payment.read",
    ];

    // Kimlik scope'ları (offline_access = sessiz yenileme, SC-003).
    public static readonly string[] IdentityScopes = ["openid", "profile", "email", "offline_access"];

    public static readonly string[] AllScopes = [.. IdentityScopes, .. ApiScopes];

    // İzinli grant'lar — client_credentials ASLA verilmez (R2 güvenlik sınırı).
    public static readonly string[] AllowedGrantTypes = ["authorization_code", "refresh_token"];

    // Claude callback'leri (loopback kalıpları DcrRequestValidator'da host bazlı denetlenir).
    public static readonly string[] AllowedExactRedirectUris =
    [
        "https://claude.ai/api/mcp/auth_callback",
        "https://claude.com/api/mcp/auth_callback",
    ];
}