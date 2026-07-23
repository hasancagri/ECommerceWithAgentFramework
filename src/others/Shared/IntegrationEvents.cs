namespace Shared;

public static class IntegrationEvents
{
    public record OrderCreatedEvent(Guid OrderId, Guid UserId, decimal TotalPrice);
    public record ProductCreatedEvent(IReadOnlyList<ProductStockInfo> Products);

    // 003-storefront-read-model: writer-publishes, fat event'ler (Storefront pull-back yapmaz).
    // 006-home-storefront-list: Description/Price/Brand eklendi; Brand enum adı string taşınır
    // (tüketici Catalog'un enum semantiğine bağlanmaz, ihtiyaç yalnız görüntüleme).
    public record ProductChangedEvent(
        Guid ProductId,
        string Name,
        string Description,
        decimal Price,
        string Brand,
        string? ImageUrl,
        bool IsDeleted);
    public record StockChangedEvent(Guid ProductId, int Quantity);
    public record DiscountChangedEvent(Guid ProductId, decimal? Rate);
}