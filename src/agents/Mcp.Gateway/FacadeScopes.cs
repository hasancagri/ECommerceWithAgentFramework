using Common.Utils.Constants;

namespace Mcp.Gateway;

// 073/079: fasad yüzey scope demetleri (challenge + PRM'de ilan edilir; gerçek yetki downstream'de +
// kullanıcı rolünde). Program.cs orkestrasyon dışı tutulur.
public static class FacadeScopes
{
    // /mcp müşteri yüzeyi scope demeti.
    public static readonly string[] Customer =
    [
        AuthorizationScopes.BasketRead, AuthorizationScopes.BasketWrite,
        AuthorizationScopes.OrderRead, AuthorizationScopes.OrderWrite,
        AuthorizationScopes.CustomerRead, AuthorizationScopes.CustomerWrite,
        AuthorizationScopes.PaymentRead, AuthorizationScopes.StorefrontRead,
    ];

    // /mcp-admin yönetim yüzeyi scope demeti.
    // 079: AdminDiscountWrite PRM'de ilan edilmezse mcp-remote scope'u istemez → token discount.api
    // audience taşımaz → discount-api /mcp-admin 401. Bu yüzden demette.
    public static readonly string[] Admin =
    [
        AuthorizationScopes.AdminCatalogRead, AuthorizationScopes.AdminCatalogWrite,
        AuthorizationScopes.StockWrite, AuthorizationScopes.MerchantCredentialsWrite,
        AuthorizationScopes.AdminDiscountWrite,
    ];
}