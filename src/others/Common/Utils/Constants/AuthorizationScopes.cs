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

    // payment.api
    public const string PaymentRead = "payment.read";
    public const string PaymentWrite = "payment.write";

    // stock.api
    public const string StockWrite = "stock.write";
    // 012: sepete ekleme/siparis aninda Basket/Order -> Stock gRPC rezervasyonu icin.
    public const string StockReserve = "stock.reserve";

    // file.api
    public const string FileWrite = "file.write";

    // storefront.api
    public const string StorefrontRead = "storefront.read";

    // customer.api (022): kayitli kart (Wallet) + adres defteri (AddressBook)
    public const string CustomerRead = "customer.read";
    public const string CustomerWrite = "customer.write";
}