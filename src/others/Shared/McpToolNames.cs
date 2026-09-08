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
    public const string GetDefaultCardBin = "get_default_card_bin";
    // 038: odeme baglami (kart vault token + gercek buyer; A2A istegine verbatim tasinir) +
    // kart listesi (kart secimi icin). 033 taksit/cekim tool'lari SOKULDU — A2A uzerinden.
    public const string GetPaymentContext = "get_payment_context";
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