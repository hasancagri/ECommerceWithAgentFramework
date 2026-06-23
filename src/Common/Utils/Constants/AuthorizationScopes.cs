namespace Common.Utils.Constants;

// Her servis icin "read" (list/detay/sorgu) ve "write" (create/update/delete) olmak uzere
// iki scope. Hem policy uretiminde (AuthenticationExtension) hem de endpoint'lerdeki
// RequireAuthorization cagrilarinda tek kaynak olarak kullanilir; boylece tanim ile kullanim
// asla birbirinden kopmaz ve yanlis yazim derleme hatasi olur.
public static class AuthorizationScopes
{
    // catalog.api
    public const string CatalogRead = "catalog.read";
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

    // discount.api
    public const string DiscountRead = "discount.read";
    public const string DiscountWrite = "discount.write";

    // stock.api
    public const string StockRead = "stock.read";
    public const string StockWrite = "stock.write";
}