using Shared.Payloads;

namespace Shared;

public static class IntegrationEvents
{
    public record OrderCreatedEvent(Guid OrderId, Guid UserId, decimal TotalPrice);
    public record ProductCreatedEvent(IReadOnlyList<ProductStockInfo> Products);
}