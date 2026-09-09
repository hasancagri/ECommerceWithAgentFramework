namespace Shared;

// MCP tool adlarının TEK kaynağı. Ad, servisler-arası SÖZLEŞMEDİR (integration-event kontratı
// emsali): sunucu attribute'ı ([McpServerTool(Name = ...)]) ile istemci (ChatAgent allowlist +
// prompt kalıpları) aynı sabiti okur — rename artık derleme hatasıyla yakalanır, sessiz kopmaz.
// DİKKAT: dış MCP istemcileri (Claude Desktop vb., 061) bu adları dışarıdan görür — sabiti
// değiştirmek DIŞ KIRILMADIR, bilinçli yapılır. Sınıf-başına-BC gruplama; DropShop onboarding
// adları BURADA DEĞİL (dış solution kontratı, ChatAgent/ConstValues.cs'te).

public static class CatalogTools
{
    public const string GetProduct = "get_product";
    public const string SearchProducts = "search_products";
    public const string GetPriceHistory = "get_price_history";
    // 067: keşif envanteri Catalog'da (envanter otoritesi; Storefront kitap-arama yüzeyi).
    public const string ListCategories = "list_categories";
    public const string ListAuthors = "list_authors";
    public const string ListPublishers = "list_publishers";
}

public static class BasketTools
{
    public const string AddToCart = "add_to_cart";
    public const string GetBasket = "get_basket";
    public const string RemoveBasketItem = "remove_basket_item";
    public const string UpdateBasketQuantity = "update_basket_quantity";
}

public static class OrderTools
{
    public const string GetOrders = "get_orders";
    // 039: chat'ten uctan uca siparis tamamlama (sunucu orkestrasyonu; cardId?/installment).
    public const string PlaceOrder = "place_order";
    // 070: sipariş öncesi taksit seçenekleri (kayıtlı kart + sepet toplamı; PG A2A quote).
    public const string QuoteInstallments = "quote_installments";
}

public static class PaymentTools
{
    public const string GetMyPayments = "get_my_payments";
}

public static class StockTools
{
    public const string GetStock = "get_stock";
}

public static class StorefrontTools
{
    // 069: tek serbest-sorgu kapısı — search_storefront_products + find_similar_books TAM İKAME silindi.
    public const string QueryStorefront = "query_storefront";
}

public static class CustomerTools
{
    // 070-sonrasi guvenlik sokumu: get_payment_context + get_default_card_bin MCP'den KALDIRILDI —
    // vault token/buyer PII agent baglamina tasiniyordu; taksit zinciri artik sunucuda
    // (OrderTools.QuoteInstallments). Odeme baglami yalniz S2S internal REST'te yasar.
    public const string ListCards = "list_cards";
    // 062: adres defteri (dış agent yüzeyi; ChatAgent allowlist'inde değil).
    public const string ListAddresses = "list_addresses";
    public const string AddAddress = "add_address";
    public const string UpdateAddress = "update_address";
    public const string RemoveAddress = "remove_address";
    public const string SetDefaultAddress = "set_default_address";
}

public static class ReviewsTools
{
    public const string GetReviews = "get_reviews";
    public const string CheckReviewEligibility = "check_review_eligibility";
    public const string SubmitReview = "submit_review";
}

public static class LibraryTools
{
    public const string GetPriceAlarm = "get_price_alarm";
    public const string CreatePriceAlarm = "create_price_alarm";
    public const string RemovePriceAlarm = "remove_price_alarm";
}

// 070: admin yönetim tool'ları — her BC'nin KORUMALI /mcp-admin ucunda yayınlanır
// (anonim /mcp keşif setine GİRMEZ). Yazma tool'ları tek-kayıt işler + AdminActionLog izi bırakır.

public static class CatalogAdminTools
{
    public const string ListProducts = "admin_list_products";
    public const string GetProduct = "admin_get_product";
    public const string UpdateProduct = "admin_update_product";
    public const string SetPublished = "admin_set_published";
    public const string GetPriceHistory = "admin_get_price_history";
}

public static class StockAdminTools
{
    public const string SetStock = "admin_set_stock";
    public const string AdjustStock = "admin_adjust_stock";
}

public static class CustomerAdminTools
{
    public const string GetMerchantStatus = "admin_get_merchant_status";
    public const string SetMerchantCredentials = "admin_set_merchant_credentials";
    // FR-016: DropShop onboarding sarmalayıcıları — makine kimliği sunucu içinde taşınır.
    public const string SubmitOnboarding = "admin_submit_onboarding";
    public const string OnboardingStatus = "admin_onboarding_status";
}

// 061: korumalı MCP'lerdeki ortak oturum-kapatma tool'u (basket/order/payment/customer).
public static class AuthTools
{
    public const string Logout = "logout";
}

public static class MailTools
{
    // 060: Mail.Mcp'nin tek tool'u; tüketici NotificationAgent (ChatAgent'a KAYITLI DEĞİL).
    public const string SendMail = "send_mail";
}