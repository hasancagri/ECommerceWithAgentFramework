namespace Supplier.Api.Domains.Feeds;

// Dataset dosyasındaki kanonik kayıt (DTO — Marten dokümanı DEĞİL, simülatör DB'siz; R12).
// Feed ucu products.json'u bu tipe çözüp olduğu gibi döner; IngestionAgent aynı şemayı okur.
public record SupplierProduct(
    string ExternalId,
    string Name,
    string Description,
    string Brand,
    decimal Price,
    int StockQuantity,
    string? DiscountCode,
    decimal? DiscountPercent);