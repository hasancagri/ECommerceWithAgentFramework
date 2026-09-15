namespace Identity.Server.Connect;

// 061: Dış agent (DCR) istemcilerinin kapalı scope demeti + izinli redirect kalıpları.
// Tek kaynak: specs/061-external-mcp-oauth/data-model.md. Yönetim scope'ları buraya GİREMEZ.
public static class ExternalAgentDefaults
{
    // Alışveriş yaşam döngüsü: arama→sepet→sipariş→takip + profil/ödeme okuma.
    public static readonly string[] ApiScopes =
    [
        AuthorizationScopes.StorefrontRead,
        AuthorizationScopes.BasketRead, AuthorizationScopes.BasketWrite,
        AuthorizationScopes.OrderRead, AuthorizationScopes.OrderWrite,
        // 062: customer.write dış agent'a adres yazma (ekle/sil/varsayılan) için açıldı.
        // UYARI: kart-yazma MCP tool'u bu scope ile AÇILMAMALI (kart mağazanın işi değil — ACP/PSP).
        AuthorizationScopes.CustomerRead, AuthorizationScopes.CustomerWrite,
        AuthorizationScopes.PaymentRead,
        // 064: reviews.write — dış agent yorum gönderme + yorum-hakkı kontrolü (get_reviews login yeter).
        AuthorizationScopes.ReviewsWrite,
        // 065: library fiyat alarmı — durum okuma + kurma/kaldırma.
        AuthorizationScopes.LibraryRead, AuthorizationScopes.LibraryWrite,
    ];

    // Kimlik scope'ları (offline_access = sessiz yenileme, SC-003).
    public static readonly string[] IdentityScopes =
        [Scopes.OpenId, Scopes.Profile, Scopes.Email, Scopes.OfflineAccess];

    public static readonly string[] AllScopes = [.. IdentityScopes, .. ApiScopes];

    // İzinli grant'lar — client_credentials ASLA verilmez (R2 güvenlik sınırı).
    public static readonly string[] AllowedGrantTypes = ["authorization_code", "refresh_token"];

    // Claude callback'leri (loopback kalıpları DcrRequestValidator'da host bazlı denetlenir).
    // Küme seed'li dış-agent istemcileriyle ortak — tek kaynak Config.
    public static readonly string[] AllowedExactRedirectUris = Config.ClaudeCallbackRedirectUris;
}