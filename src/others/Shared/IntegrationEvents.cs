namespace Shared;

public static class IntegrationEvents
{
    public record OrderCreatedEvent(Guid OrderId, Guid UserId, decimal TotalPrice);
    public record ProductCreatedEvent(IReadOnlyList<ProductStockInfo> Products);

    // 003-storefront-read-model: writer-publishes, fat event'ler (Storefront pull-back yapmaz).
    public record ProductChangedEvent(Guid ProductId, string Name, string? ImageUrl, bool IsDeleted, DateTime OccurredAtUtc);
    public record StockChangedEvent(Guid ProductId, bool IsInStock, DateTime OccurredAtUtc);
    public record DiscountChangedEvent(Guid ProductId, decimal? Rate, DateTime OccurredAtUtc);
}