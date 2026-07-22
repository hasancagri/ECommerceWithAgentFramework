namespace IngestionAgent.Domains;

// Feed'deki tek kayıt: Supplier.Api'nin SupplierProduct şemasının birebir karşılığı. Hem telden
// doğrudan deserialize edilir hem staging'e Normalized olarak yazılır (tek model). record olduğu
// için değer eşitliği bedava: "içerik değişti mi?" kıyası hash'siz, doğrudan == ile yapılır.
public sealed record FeedRecord(
    string ExternalId,
    string Name,
    string Description,
    string Brand,
    decimal Price,
    int StockQuantity,
    string? DiscountCode,
    decimal? DiscountPercent);