using Duende.IdentityServer.Models;

namespace Identity.Server;

public static class Config
{
    // Servislerin token'da bekledigi ekstra claim'ler (role/email policy'leri icin).
    private static readonly string[] ApiUserClaims = ["role", "email", "name"];

    public static IEnumerable<IdentityResource> IdentityResources =>
    [
        new IdentityResources.OpenId(),
        new IdentityResources.Profile(),
        new IdentityResources.Email(),
        // id_token/userinfo'ya role claim'i tasimak icin.
        new IdentityResource("roles", "Roller", ["role"]),
    ];

    // ApiScope = servis basina read/write yetki birimi.
    // read = liste/detay/sorgu, write = olustur/guncelle/sil.
    public static IEnumerable<ApiScope> ApiScopes =>
    [
        // catalog.api (okuma anonim — scope yok; yalnizca yazma korunur)
        new ApiScope("catalog.write", "Catalog API - yazma (olustur/guncelle/sil)"),

        // basket.api
        new ApiScope("basket.read", "Basket API - okuma"),
        new ApiScope("basket.write", "Basket API - yazma"),

        // order.api
        new ApiScope("order.read", "Order API - okuma"),
        new ApiScope("order.write", "Order API - yazma"),

        // payment.api
        new ApiScope("payment.read", "Payment API - okuma"),
        new ApiScope("payment.write", "Payment API - yazma"),

        // discount.api
        new ApiScope("discount.read", "Discount API - okuma"),
        new ApiScope("discount.write", "Discount API - yazma"),

        // stock.api
        new ApiScope("stock.write", "Stock API - yazma (artir/azalt)"),

        // file.api: gorsel upload MCP tool'unu korur.
        new ApiScope("file.write", "File API - yazma (gorsel upload)"),

        // storefront.api: herkese acik urun-vitrin gorunumu (yine de anonim-M2M scope ister).
        new ApiScope("storefront.read", "Storefront API - okuma (urun vitrin gorunumu)"),

        // identity: UserKey (ApiKeys) yonetim yetkisi — admin issue/revoke uclarini korur.
        new ApiScope("apikeys.manage", "API Key yonetimi (admin issue/revoke)"),
    ];

    // ApiResource adi = servisin dogruladigi Audience (appsettings IdentityOption.Audience).
    // Token'in 'aud' claim'i bu ada esitlenir; uyusmazsa servis token'i reddeder.
    public static IEnumerable<ApiResource> ApiResources =>
    [
        new ApiResource("catalog.api", "Catalog API")
        {
            Scopes = { "catalog.write" },
            UserClaims = ApiUserClaims,
        },
        new ApiResource("basket.api", "Basket API")
        {
            Scopes = { "basket.read", "basket.write" },
            UserClaims = ApiUserClaims,
        },
        new ApiResource("order.api", "Order API")
        {
            Scopes = { "order.read", "order.write" },
            UserClaims = ApiUserClaims,
        },
        new ApiResource("payment.api", "Payment API")
        {
            Scopes = { "payment.read", "payment.write" },
            UserClaims = ApiUserClaims,
        },
        new ApiResource("discount.api", "Discount API")
        {
            Scopes = { "discount.read", "discount.write" },
            UserClaims = ApiUserClaims,
        },
        new ApiResource("stock.api", "Stock API")
        {
            Scopes = { "stock.write" },
            UserClaims = ApiUserClaims,
        },
        // file.api: MCP upload yuzeyi file.write scope'uyla korunur.
        new ApiResource("file.api", "File API")
        {
            Scopes = { "file.write" },
            UserClaims = ApiUserClaims,
        },
        new ApiResource("storefront.api", "Storefront API")
        {
            Scopes = { "storefront.read" },
            UserClaims = ApiUserClaims,
        },
    ];

    public static IEnumerable<Client> Clients =>
    [
        // Anonim/giris yapmamis kullanici icin uygulamanin kendi kimligi (public okuma).
        new Client
        {
            ClientId = "m2m.client",
            ClientName = "Machine-to-machine test client",
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            ClientSecrets = { new Secret("dev-secret".Sha256()) },
            AllowedScopes =
            {
                "discount.read",
            },
        },
        // Admin m2m: UserKey issue/revoke uclarini cagirmak icin apikeys.manage tasir.
        // (v1'de uclar X-Internal-Secret ile korunur; bu client uretim scope-korumasi icin hazir.)
        new Client
        {
            ClientId = "apikeys.admin",
            ClientName = "API Key admin (m2m)",
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            ClientSecrets = { new Secret("apikeys-admin-secret".Sha256()) },
            AllowedScopes = { "apikeys.manage" },
        },
        // WebApp (Razor Pages BFF): kullanici login'i icin Authorization Code,
        // anonim okuma icin de Client Credentials.
        new Client
        {
            ClientId = "ecommerce.bff",
            ClientName = "ECommerce (Razor Pages BFF)",
            AllowedGrantTypes = GrantTypes.CodeAndClientCredentials,
            ClientSecrets = { new Secret("webshop-secret".Sha256()) },
            // WebApp'in calistigi URL (launchSettings https profili). Aspire farkli port
            // atarsa buraya o URL'i de eklemek gerekir; OIDC redirect birebir eslesmeli.
            RedirectUris = { "https://localhost:7042/signin-oidc" },
            PostLogoutRedirectUris = { "https://localhost:7042/signout-callback-oidc" },
            RequireConsent = false,
            AllowOfflineAccess = true,
            // role/email/name claim'lerini id_token'a koy ki WebApp principal'inda olsun.
            AlwaysIncludeUserClaimsInIdToken = true,
            AllowedScopes =
            {
                "openid", "profile", "email", "roles",
                "catalog.write",
                "basket.read", "basket.write",
                "order.read", "order.write",
                "payment.read", "payment.write",
                "discount.read", "discount.write",
                "stock.write",
                "storefront.read",
            },
        },
    ];
}