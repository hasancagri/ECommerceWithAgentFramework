namespace Shared;

public static class IntegrationEvents
{
    public record OrderCreatedEvent(Guid OrderId, Guid UserId, decimal TotalPrice);
    public record ProductCreatedEvent(IReadOnlyList<ProductStockInfo> Products);

    // 003-storefront-read-model: writer-publishes, fat event'ler (Storefront pull-back yapmaz).
    public record ProductChangedEvent(Guid ProductId, string Name, string? ImageUrl, bool IsDeleted);
    public record StockChangedEvent(Guid ProductId, int Quantity);
    public record DiscountChangedEvent(Guid ProductId, decimal? Rate);
}