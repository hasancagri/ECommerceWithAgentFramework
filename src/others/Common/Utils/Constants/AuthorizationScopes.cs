namespace Common.Utils.Constants;

public static class AuthorizationScopes
{
    // catalog.api (okuma anonim — read scope'u yok)
    public const string CatalogWrite = "catalog.write";

    // basket.api
    public const string BasketRead = "basket.read";
    public const string BasketWrite = "basket.write";

    // order.api
    public const string OrderRead = "order.read";
    public const string OrderWrite = "order.write";

    // checkout.orchestrator (049): checkout giriş endpoint'i kullanıcı scope'u (tıkla/yaz aynı süreç).
    // Broker komut handler'ları scope-guard DEĞİL (HttpContext yok); yalnız HTTP giriş korunur.
    public const string CheckoutWrite = "checkout.write";

    // payment.api
    public const string PaymentRead = "payment.read";
    public const string PaymentWrite = "payment.write";

    // stock.api
    public const string StockWrite = "stock.write";
    // 012: sepete ekleme/siparis aninda Basket/Order -> Stock gRPC rezervasyonu icin.
    public const string StockReserve = "stock.reserve";

    // storefront.api
    public const string StorefrontRead = "storefront.read";

    // customer.api (022): kayitli kart (Wallet) + adres defteri (AddressBook)
    public const string CustomerRead = "customer.read";
    public const string CustomerWrite = "customer.write";
    // DropShop vault merchant kimligi (merchantId+key) yonetimi — admin-only capability (customer HARIC).
    // Audience customer.api (endpoint Customer.Api'de). Onboarding'de admin'e verilen kimligi girer.
    public const string MerchantCredentialsWrite = "merchant.credentials.write";

    // reviews.api (044): yorum yazma + uygunluk sorgusu; Order satin-alma-kaniti gRPC ucu da
    // bu scope'u ister (R4: ayri scope acilmaz, sub==user_id guard'i sunucuda).
    public const string ReviewsWrite = "reviews.write";

    // identity (030 RBAC): IdP rol/scope/kullanici yonetim yuzeyi. Downstream servis zorlamaz;
    // Identity.Server ic yuzeyini + WebApp header link gorunurlugunu belirler.
    public const string IdentityRolesManage = "identity.roles.manage";

    // personalization.api (048): gezinme sinyali ingest ucu. WebApp (BFF) client_credentials
    // MAKINE kimligiyle sunar (anonim gezinme user token tasimaz); son-kullanici kimligi payload'da.
    public const string PersonalizationIngest = "personalization.ingest";
}