namespace Shared;

public static class IntegrationEvents
{
    public record OrderCreatedEvent(Guid OrderId, Guid UserId, decimal TotalPrice);

    // 003-storefront-read-model: writer-publishes, fat event'ler (Storefront pull-back yapmaz).
    // 006-home-storefront-list: Description/Price/Brand eklendi.
    // 016-category-brand: kimlik + ad birlikte taşınır (R7); Id opak değerdir, tüketici lookup yapmaz.
    public record ProductChangedEvent(
        Guid ProductId,
        string Name,
        string Description,
        decimal Price,
        Guid BrandId,
        string Brand,
        Guid? CategoryId,
        string? Category,
        string? ImageUrl,
        bool IsDeleted);
    public record StockChangedEvent(Guid ProductId, int Quantity);
    public record DiscountChangedEvent(Guid ProductId, decimal? Rate);

    // 012-stock-reservation: TTL dolunca Stock yayinlar; Basket ilgili sepet satirini siler.
    public record ReservationExpired(Guid ProductId, Guid UserId);

    // 007-supplier-gateway: kaydın tedarikçideki GÜNCEL hali (snapshot, diff değil).
    // Tedarikçi kimliği tip değil alandır (SupplierCode); DiscountPercent null = indirim yok → remove.
    // 016: Category null = kategorisiz (FR-010 toleransı).
    public record SupplierProductSnapshotReceived(
        string SupplierCode,
        string ExternalId,
        string Name,
        string Description,
        string Brand,
        string? Category,
        decimal Price,
        int StockQuantity,
        decimal? DiscountPercent);
}