namespace Identity.Server;

// Duende in-memory modelleri yerine düz seed sabitleri. SeedHostedService bunları
// açılışta OpenIddict application/scope manager'larına idempotent yazar.
public static class Config
{
    // Identity scope'ları (openid/profile/email + role taşıyıcı "roles").
    public static readonly string[] IdentityScopes =
        [Scopes.OpenId, Scopes.Profile, Scopes.Email, Scopes.Roles, Scopes.OfflineAccess];

    // 070: seed'li dış-agent YÖNETİM istemcisi (Claude Desktop admin bağlantısı). DCR yüzeyinin
    // tamamen DIŞINDA — ExternalAgentDefaults/DcrRequestValidator değişmez; loopback redirect
    // muafiyeti AdminAgentApplicationManager'da bu ClientId'ye özeldir.
    public const string ExternalAdminAgentClientId = "external-admin-agent";

    // 073: seed'li dış MÜŞTERİ agent istemcisi (tek müşteri MCP fasadı; Claude Desktop müşteri bağlantısı).
    // Loopback redirect muafiyeti external-admin-agent ile aynı (AdminAgentApplicationManager).
    public const string ExternalCustomerAgentClientId = "external-customer-agent";

    // Claude sabit callback'leri — seed'li dış-agent istemcileri + DCR izinli-liste
    // (ExternalAgentDefaults.AllowedExactRedirectUris) aynı kümeyi paylaşır.
    public static readonly string[] ClaudeCallbackRedirectUris =
    [
        "https://claude.ai/api/mcp/auth_callback",
        "https://claude.com/api/mcp/auth_callback",
    ];

    // Scope → audience (resource) haritası. Token üretiminde ListResourcesAsync bu eşlemeden
    // 'aud' claim'ini üretir; servisler kendi adını (basket.api...) ValidateAudience ile arar.
    public static readonly IReadOnlyDictionary<string, string> ScopeResources =
        new Dictionary<string, string>
        {
            [AuthorizationScopes.AdminCatalogRead] = "catalog.api",
            [AuthorizationScopes.AdminCatalogWrite] = "catalog.api",
            [AuthorizationScopes.BasketRead] = "basket.api",
            [AuthorizationScopes.BasketWrite] = "basket.api",
            [AuthorizationScopes.OrderRead] = "order.api",
            [AuthorizationScopes.OrderWrite] = "order.api",
            [AuthorizationScopes.PaymentRead] = "payment.api",
            [AuthorizationScopes.PaymentWrite] = "payment.api",
            [AuthorizationScopes.StockWrite] = "stock.api",
            [AuthorizationScopes.StorefrontRead] = "storefront.api",
            [AuthorizationScopes.CustomerRead] = "customer.api",
            [AuthorizationScopes.CustomerWrite] = "customer.api",
            // DropShop vault merchant kimliği yönetimi — audience customer.api; admin demetinde (AllApiScopes),
            // customer'da YOK. AllApiScopes bu key'i otomatik alır → admin role'a düşer.
            [AuthorizationScopes.MerchantCredentialsWrite] = "customer.api",
            // 044: yorum yazma (Order purchase-check gRPC ucu da aynı scope'u ister — R4).
            [AuthorizationScopes.ReviewsWrite] = "reviews.api",
            // 060: fiyat alarmı (Library BC) — durum okuma + kurma/kaldırma.
            [AuthorizationScopes.LibraryRead] = "library.api",
            [AuthorizationScopes.LibraryWrite] = "library.api",
        };

    // Tüm API scope'ları (servis scope'ları + apikeys.manage + identity.roles.manage) — seed edilir.
    // 030: KnownScopes registry bu listeyi tek kaynak olarak kullanır (atanabilir scope kümesi).
    public static IEnumerable<string> AllApiScopes =>
        ScopeResources.Keys.Append(AuthorizationScopes.ApiKeysManage).Append(AuthorizationScopes.IdentityRolesManage);

    // 030 RBAC seed rol demetleri (KnownScopes ⊇ bunlar). Admin ⊇ customer + yönetim/yazma.
    // customer: müşteri akışı (katalog yazma / stok mutlak yazma / api-key / rol yönetimi HARİÇ).
    public static readonly string[] CustomerRoleScopes =
    [
        AuthorizationScopes.BasketRead, AuthorizationScopes.BasketWrite,
        AuthorizationScopes.OrderRead, AuthorizationScopes.OrderWrite,
        AuthorizationScopes.PaymentRead, AuthorizationScopes.PaymentWrite,
        AuthorizationScopes.StorefrontRead,
        AuthorizationScopes.CustomerRead, AuthorizationScopes.CustomerWrite,
        AuthorizationScopes.ReviewsWrite,
        // 060: fiyat alarmı müşteri akışının parçası.
        AuthorizationScopes.LibraryRead, AuthorizationScopes.LibraryWrite,
    ];

    // admin: tüm atanabilir scope'lar (customer + catalog.write + stock.write + apikeys.manage + identity.roles.manage).
    public static IEnumerable<string> AdminRoleScopes => AllApiScopes;

    // Seed edilecek roller ve scope demetleri (rol adı → scope'lar). SeedHostedService idempotent yazar.
    public static IReadOnlyDictionary<string, string[]> RoleScopeSeed =>
        new Dictionary<string, string[]>
        {
            [RoleAssignmentService.CustomerRole] = CustomerRoleScopes,
            [RoleAssignmentService.AdminRole] = [.. AdminRoleScopes],
        };

    // İstemci kayıtları (secret düz değer; store hash'ler — WebApp/SagaTokenHandler config'i değişmez).
    public static IReadOnlyList<ClientSeed> Clients =>
    [
        // Admin m2m: UserKey issue/revoke uçlarını apikeys.manage ile korur.
        new ClientSeed
        {
            ClientId = "apikeys.admin",
            ClientSecret = "apikeys-admin-secret",
            DisplayName = "API Key admin (m2m)",
            AllowClientCredentials = true,
            Scopes = [AuthorizationScopes.ApiKeysManage],
        },
        // 028: checkout saga m2m — arka planda koşar (kullanıcı bearer'ı taşınamaz).
        new ClientSeed
        {
            ClientId = "order-saga",
            ClientSecret = "order-saga-secret",
            DisplayName = "Checkout saga (m2m)",
            AllowClientCredentials = true,
            // 028/056: basket.write; 039: basket.read (kalem okuma) + customer.read (adres/odeme baglami);
            // 077: payment.write (hosted-CF start_payment → Payment.Api link isteği S2S).
            Scopes =
            [
                AuthorizationScopes.BasketWrite, AuthorizationScopes.BasketRead,
                AuthorizationScopes.CustomerRead, AuthorizationScopes.PaymentWrite,
            ],
        },
        // 077: Payment.Api m2m — hosted-CF link isteği/callback arka planda Customer merchant-key okur.
        new ClientSeed
        {
            ClientId = "payment-s2s",
            ClientSecret = "payment-s2s-secret",
            DisplayName = "Payment S2S (m2m)",
            AllowClientCredentials = true,
            Scopes = [AuthorizationScopes.CustomerRead],
        },
        // 070: dış-agent YÖNETİM istemcisi — public+PKCE, code+refresh; consent Implicit (mağaza
        // sahibinin kendi aracı). Scope TAVANI yönetim demeti; gerçek yetki = tavan ∩ kullanıcı ROL
        // demeti (030) — admin-olmayan kullanıcı bu istemciyle girse de yönetim scope'u ALAMAZ.
        // Redirect: Claude callback'leri sabit; loopback (http://localhost|127.0.0.1, her port)
        // AdminAgentApplicationManager.ValidateRedirectUriAsync muafiyetiyle (RFC 8252 §7.3).
        new ClientSeed
        {
            ClientId = ExternalAdminAgentClientId,
            ClientSecret = null,
            DisplayName = "External admin agent (Claude Desktop)",
            IsPublic = true,
            AllowAuthorizationCode = true,
            AllowRefreshToken = true,
            RedirectUris = ClaudeCallbackRedirectUris,
            Scopes =
            [
                Scopes.OpenId, Scopes.Profile,
                AuthorizationScopes.StorefrontRead, AuthorizationScopes.AdminCatalogRead,
                AuthorizationScopes.AdminCatalogWrite, AuthorizationScopes.StockWrite,
                AuthorizationScopes.MerchantCredentialsWrite,
            ],
        },
        // 073: tek müşteri MCP fasadı — dış müşteri agent kimliği (public+PKCE, Explicit consent). Tek
        // consent = tek login; müşteri scope demeti (gerçek yetki = demet ∩ kullanıcı rolü). Redirect
        // loopback (mcp-remote dinamik port) AdminAgentApplicationManager muafiyetiyle + Claude callback.
        new ClientSeed
        {
            ClientId = ExternalCustomerAgentClientId,
            ClientSecret = null,
            DisplayName = "External customer agent (Claude Desktop)",
            IsPublic = true,
            AllowAuthorizationCode = true,
            AllowRefreshToken = true,
            RedirectUris = ClaudeCallbackRedirectUris,
            Scopes =
            [
                Scopes.OpenId, Scopes.Profile,
                AuthorizationScopes.BasketRead, AuthorizationScopes.BasketWrite,
                AuthorizationScopes.OrderRead, AuthorizationScopes.OrderWrite,
                AuthorizationScopes.CustomerRead, AuthorizationScopes.CustomerWrite,
                AuthorizationScopes.PaymentRead, AuthorizationScopes.StorefrontRead,
            ],
        },
        // 073: fasad keşif (ListTools) makine kimliği — client_credentials, salt audience üretimi
        // (tool ÇAĞRISI her zaman kullanıcı token'ıyla; bu token'la çalıştırılmaz). ChatAgent discovery emsali.
        new ClientSeed
        {
            ClientId = "mcp-gateway-discovery",
            ClientSecret = "mcp-gateway-discovery-secret",
            DisplayName = "MCP Gateway discovery (m2m)",
            AllowClientCredentials = true,
            Scopes =
            [
                AuthorizationScopes.BasketRead, AuthorizationScopes.OrderRead,
                AuthorizationScopes.CustomerRead, AuthorizationScopes.PaymentRead,
                AuthorizationScopes.AdminCatalogRead,
                AuthorizationScopes.StockWrite, AuthorizationScopes.MerchantCredentialsWrite,
            ],
        },
        // WebApp (Razor Pages BFF) SÖKÜLDÜ (2026-09-11) — UI kaldırıldı, agent-only. ecommerce.bff
        // istemcisi + WebApp redirect URI'ları kalktı; müşteri/admin agent'ları external-*-agent ile girer.
    ];
}

// Tek istemci seed tanımı (Duende Client'ın düz karşılığı).
public sealed class ClientSeed
{
    public required string ClientId { get; init; }
    // 070: public (PKCE) istemcide secret YOK — null bırakılır.
    public required string? ClientSecret { get; init; }
    public required string DisplayName { get; init; }
    // 070: public istemci (secret'sız + PKCE zorunlu); confidential seed'ler için false kalır.
    public bool IsPublic { get; init; }
    public bool AllowAuthorizationCode { get; init; }
    public bool AllowClientCredentials { get; init; }
    public bool AllowRefreshToken { get; init; }
    public string[] RedirectUris { get; init; } = [];
    public string[] Scopes { get; init; } = [];
}