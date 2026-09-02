namespace Identity.Server;

// Duende in-memory modelleri yerine düz seed sabitleri. SeedHostedService bunları
// açılışta OpenIddict application/scope manager'larına idempotent yazar.
public static class Config
{
    // WebApp'in redirect/logout URI'ları (launchSettings https profili).
    public const string WebAppRedirectUri = "https://localhost:7042/signin-oidc";
    public const string WebAppPostLogoutRedirectUri = "https://localhost:7042/signout-callback-oidc";

    // Kullanıcı token'larına taşınan claim'ler (WebApp + servis policy'leri okur).
    public static readonly string[] UserClaims = ["role", "email", "name"];

    // Identity scope'ları (openid/profile/email + role taşıyıcı "roles").
    public static readonly string[] IdentityScopes = ["openid", "profile", "email", "roles", "offline_access"];

    // apikeys.manage: Identity.Server kendi Bearer policy'siyle doğrular (audience'sız).
    public const string ApiKeysManageScope = "apikeys.manage";

    // 030 RBAC: rol/scope/kullanıcı yönetim yüzeyi scope'u (audience'sız; IdP iç yüzeyi + WebApp link).
    public const string IdentityRolesManageScope = "identity.roles.manage";

    // Scope → audience (resource) haritası. Token üretiminde ListResourcesAsync bu eşlemeden
    // 'aud' claim'ini üretir; servisler kendi adını (basket.api...) ValidateAudience ile arar.
    public static readonly IReadOnlyDictionary<string, string> ScopeResources =
        new Dictionary<string, string>
        {
            ["catalog.write"] = "catalog.api",
            ["basket.read"] = "basket.api",
            ["basket.write"] = "basket.api",
            ["order.read"] = "order.api",
            ["order.write"] = "order.api",
            ["payment.read"] = "payment.api",
            ["payment.write"] = "payment.api",
            ["stock.write"] = "stock.api",
            ["storefront.read"] = "storefront.api",
            ["customer.read"] = "customer.api",
            ["customer.write"] = "customer.api",
            // DropShop vault merchant kimliği yönetimi — audience customer.api; admin demetinde (AllApiScopes),
            // customer'da YOK. AllApiScopes bu key'i otomatik alır → admin role'a düşer.
            ["merchant.credentials.write"] = "customer.api",
            // 044: yorum yazma (Order purchase-check gRPC ucu da aynı scope'u ister — R4).
            ["reviews.write"] = "reviews.api",
            // 060: fiyat alarmı (Library BC) — durum okuma + kurma/kaldırma.
            ["library.read"] = "library.api",
            ["library.write"] = "library.api",
            // 053: gezinme sinyali ingest + profil okuma — audience reco.trainer (Python beyin; 048
            // personalization.api emekli). WebApp (BFF) m2m istemcisi (webapp-signals) client_credentials
            // ile talep eder; customer/admin kullanıcı token'ına binmez.
            ["personalization.ingest"] = "reco.trainer",
            ["personalization.read"] = "reco.trainer",
        };

    // WebApp BFF'nin talep ettiği 12 servis scope'u (apikeys.manage HARİÇ; bugünkü Duende paritesi).
    public static readonly string[] BffServiceScopes =
    [
        "catalog.write",
        "basket.read", "basket.write",
        "order.read", "order.write",
        "payment.read", "payment.write",
        "stock.write",
        "storefront.read",
        "customer.read", "customer.write",
        // Admin kullanıcı token'ı bunu taşısın diye BFF ister; yalnız admin role demeti verir (customer'da yok).
        "merchant.credentials.write",
        // 044: yorum yazma (form + submit; Order purchase-check gRPC'si de bunu ister).
        "reviews.write",
        // 060: fiyat alarmı düğmesi (durum + kur/kaldır).
        "library.read", "library.write",
    ];

    // Tüm API scope'ları (12 servis scope'u + apikeys.manage + identity.roles.manage) — seed edilir.
    // 030: KnownScopes registry bu listeyi tek kaynak olarak kullanır (atanabilir scope kümesi).
    public static IEnumerable<string> AllApiScopes =>
        ScopeResources.Keys.Append(ApiKeysManageScope).Append(IdentityRolesManageScope);

    // 030 RBAC seed rol demetleri (KnownScopes ⊇ bunlar). Admin ⊇ customer + yönetim/yazma.
    // customer: müşteri akışı (katalog yazma / stok mutlak yazma / api-key / rol yönetimi HARİÇ).
    public static readonly string[] CustomerRoleScopes =
    [
        "basket.read", "basket.write",
        "order.read", "order.write",
        "payment.read", "payment.write",
        "storefront.read",
        "customer.read", "customer.write",
        "reviews.write",
        // 060: fiyat alarmı müşteri akışının parçası.
        "library.read", "library.write",
    ];

    // admin: tüm atanabilir scope'lar (customer + catalog.write + stock.write + apikeys.manage + identity.roles.manage).
    public static IEnumerable<string> AdminRoleScopes => AllApiScopes;

    // Seed edilecek roller ve scope demetleri (rol adı → scope'lar). SeedHostedService idempotent yazar.
    public static IReadOnlyDictionary<string, string[]> RoleScopeSeed =>
        new Dictionary<string, string[]>
        {
            ["customer"] = CustomerRoleScopes,
            ["admin"] = [.. AdminRoleScopes],
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
            Scopes = [ApiKeysManageScope],
        },
        // 028: checkout saga m2m — arka planda koşar (kullanıcı bearer'ı taşınamaz).
        new ClientSeed
        {
            ClientId = "order-saga",
            ClientSecret = "order-saga-secret",
            DisplayName = "Checkout saga (m2m)",
            AllowClientCredentials = true,
            // 028/056: basket.write; 039: basket.read (kalem okuma) + customer.read (odeme baglami).
            Scopes = ["basket.write", "basket.read", "customer.read"],
        },
        // 048: WebApp davranış-sinyali gönderimi m2m — anonim gezinme user token taşımaz,
        // WebApp client_credentials ile personalization.ingest talep eder (BFF telemetri iletici).
        new ClientSeed
        {
            ClientId = "webapp-signals",
            ClientSecret = "webapp-signals-secret",
            DisplayName = "WebApp behavior signals (m2m)",
            AllowClientCredentials = true,
            // 053: ingest (sinyal yaz) + read (zevk profili oku) — ikisi de reco.trainer audience.
            Scopes = ["personalization.ingest", "personalization.read"],
        },
        // 050: çok-tedarikçi feed (Procurement/Supplier + eski ingestion-agent) söküldü — first-party
        // ürün-CRUD yazım yolu, ayrı m2m istemci gerektirmez.
        // WebApp (Razor Pages BFF): yalnız kullanıcı login'i (code+PKCE+refresh, confidential).
        // 031: anonim okuma artık gerçekten anonim (storefront AllowAnonymous) → client_credentials KALKTI.
        new ClientSeed
        {
            ClientId = "ecommerce.bff",
            ClientSecret = "webshop-secret",
            DisplayName = "ECommerce (Razor Pages BFF)",
            AllowAuthorizationCode = true,
            AllowRefreshToken = true,
            RedirectUris = [WebAppRedirectUri],
            PostLogoutRedirectUris = [WebAppPostLogoutRedirectUri],
            Scopes = [.. IdentityScopes, .. BffServiceScopes],
        },
    ];
}

// Tek istemci seed tanımı (Duende Client'ın düz karşılığı).
public sealed class ClientSeed
{
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
    public required string DisplayName { get; init; }
    public bool AllowAuthorizationCode { get; init; }
    public bool AllowClientCredentials { get; init; }
    public bool AllowRefreshToken { get; init; }
    public string[] RedirectUris { get; init; } = [];
    public string[] PostLogoutRedirectUris { get; init; } = [];
    public string[] Scopes { get; init; } = [];
}